namespace CommunitiesForever.Upload.Core.Site;

/// <summary>What the setup screen shows while connecting.</summary>
public abstract record ConnectProgress
{
    private ConnectProgress()
    {
    }

    public sealed record RequestingCode : ConnectProgress;

    /// <summary>
    /// The browser was opened at <paramref name="Page"/> (the complete verification URL, for "Copy the link");
    /// show <paramref name="UserCode"/> until <paramref name="ExpiresAt"/>.
    /// </summary>
    public sealed record WaitingForBrowser(string UserCode, Uri Page, DateTimeOffset ExpiresAt) : ConnectProgress;
}

public abstract record ConnectOutcome
{
    private ConnectOutcome()
    {
    }

    public sealed record Connected(SiteIdentity Identity) : ConnectOutcome;

    public sealed record Denied : ConnectOutcome;

    public sealed record Expired : ConnectOutcome;

    /// <summary>A pasted token that is not <c>cfu_</c> and 43 characters.</summary>
    public sealed record MalformedToken : ConnectOutcome;

    /// <summary>The site does not know the token (pasted) or refused it.</summary>
    public sealed record UnknownToken : ConnectOutcome;

    public sealed record Failed(SiteFailure Failure) : ConnectOutcome;
}

public sealed record DisconnectOutcome(bool RevokedOnSite);

/// <summary>
/// Connecting (RFC 8628 device flow, UPLOADER-API.md "Connecting"), pasting a token by hand, and disconnecting.
/// The device code lives only in this method's locals; the token is stored only once <c>/me</c> accepted it.
/// </summary>
public sealed class ConnectFlow(ISiteClient site, ITokenStore tokens, IBrowser browser, IDeviceInfo device, TimeProvider time)
{
    // RFC 8628 §3.5: default interval, and what slow_down adds.
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    public async Task<ConnectOutcome> ConnectAsync(IProgress<ConnectProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new ConnectProgress.RequestingCode());
        var requested = await site.RequestDeviceCodeAsync(device.DeviceName, cancellationToken).ConfigureAwait(false);
        if (!requested.IsOk)
        {
            return new ConnectOutcome.Failed(requested.Failure!);
        }

        var authorization = requested.Value!;
        var expiresAt = time.GetUtcNow() + authorization.ExpiresIn;
        var interval = authorization.Interval > TimeSpan.Zero ? authorization.Interval : DefaultInterval;
        var page = authorization.VerificationUriComplete ?? authorization.VerificationUri;
        if (!IsSafe(page))
        {
            return new ConnectOutcome.Failed(new SiteFailure(SiteError.Unexpected, "unsafe_verification_uri"));
        }

        browser.Open(page);
        progress?.Report(new ConnectProgress.WaitingForBrowser(authorization.UserCode, page, expiresAt));

        while (true)
        {
            await Task.Delay(interval, time, cancellationToken).ConfigureAwait(false);
            if (time.GetUtcNow() >= expiresAt)
            {
                return new ConnectOutcome.Expired();
            }

            var poll = await site.PollTokenAsync(authorization.DeviceCode, cancellationToken).ConfigureAwait(false);
            if (!poll.IsOk)
            {
                switch (poll.Failure!.Kind)
                {
                    case SiteError.Offline:
                    case SiteError.ServerError:
                        continue; // keep trying until the code expires
                    case SiteError.RateLimited:
                        interval = Max(interval, poll.Failure.RetryAfter ?? interval);
                        continue;
                    default:
                        return new ConnectOutcome.Failed(poll.Failure);
                }
            }

            switch (poll.Value!.Status)
            {
                case PollStatus.Pending:
                    continue;
                case PollStatus.SlowDown:
                    interval += SlowDownStep;
                    continue;
                case PollStatus.Denied:
                    return new ConnectOutcome.Denied();
                case PollStatus.Expired:
                    return new ConnectOutcome.Expired();
                case PollStatus.Granted:
                default:
                    return await AcceptAsync(poll.Value.Token!, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>"Paste an upload token instead": format check, then <c>/me</c>, then store.</summary>
    public Task<ConnectOutcome> PasteTokenAsync(string? text, CancellationToken cancellationToken = default) =>
        AccessToken.TryParse(text, out var token)
            ? AcceptAsync(token!, cancellationToken)
            : Task.FromResult<ConnectOutcome>(new ConnectOutcome.MalformedToken());

    /// <summary>Revokes the token on the site (RFC 7009) and deletes it here, also when the site can't be reached.</summary>
    public async Task<DisconnectOutcome> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (tokens.Load() is not { } token)
        {
            return new DisconnectOutcome(RevokedOnSite: true);
        }

        var revoked = await site.RevokeAsync(token, cancellationToken).ConfigureAwait(false);
        tokens.Delete();
        return new DisconnectOutcome(revoked.IsOk);
    }

    private async Task<ConnectOutcome> AcceptAsync(AccessToken token, CancellationToken cancellationToken)
    {
        var identity = await site.GetIdentityAsync(token, cancellationToken).ConfigureAwait(false);
        if (!identity.IsOk)
        {
            return identity.Failure!.Kind == SiteError.InvalidToken
                ? new ConnectOutcome.UnknownToken()
                : new ConnectOutcome.Failed(identity.Failure);
        }

        tokens.Save(token);
        return new ConnectOutcome.Connected(identity.Value!);
    }

    // Only the site's own https pages, or http on this PC for development.
    private static bool IsSafe(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
