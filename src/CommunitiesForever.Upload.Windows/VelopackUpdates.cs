using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using CommunitiesForever.Upload.Core.Site;
using CommunitiesForever.Upload.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// Velopack against the site's update feed (UPLOADER-API.md "Installer and updates", channel <c>win</c>), with the
/// feed's signature checked first (<see cref="FeedKey"/>). Thin: when to check and install is <see cref="UpdateChecker"/>'s business.
/// </summary>
public sealed class VelopackUpdates : IAppUpdates
{
    public const string FeedPath = "api/uploader/v1/updates/";

    private readonly UpdateManager _manager;
    private readonly string _appVersion;
    private UpdateInfo? _offer;

    public VelopackUpdates(HttpClient http, ITokenStore tokens, SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var feed = new Uri(options.BaseUri, FeedPath);
        _manager = new UpdateManager(new SimpleWebSource(feed, new BearerDownloader(http, tokens, feed, FeedKey.Load())));
        _appVersion = options.AppVersion;
    }

    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? _appVersion;

    public async Task<UpdateOffer?> CheckAsync(CancellationToken cancellationToken)
    {
        _offer = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return _offer is { TargetFullRelease: var target } ? new UpdateOffer(target.Version.ToString(), target.Size, Summary(target.NotesMarkdown)) : null;
    }

    public async Task DownloadAsync(IProgress<long> progress, CancellationToken cancellationToken)
    {
        if (_offer is not { } offer)
        {
            return;
        }

        long size = offer.TargetFullRelease.Size;
        try
        {
            await _manager.DownloadUpdatesAsync(offer, percent => progress.Report(size * percent / 100), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not (OperationCanceledException or HttpRequestException or IOException or InvalidDataException))
        {
            // Velopack checks size and SHA256 against the signed feed and removes a file that doesn't match.
            throw new InvalidDataException("The download doesn't match the feed.", e);
        }
    }

    // The release notes' first line (the release workflow takes them from CHANGELOG.md).
    private static string? Summary(string? markdown) =>
        markdown?.Split('\n').Select(l => l.Trim().TrimStart('-', '*', '#', ' ')).FirstOrDefault(l => l.Length > 0);

    public void ApplyAfterExit(bool restart)
    {
        if (_manager.UpdatePendingRestart is { } pending)
        {
            _manager.WaitExitThenApplyUpdates(pending, silent: true, restart, restart ? [RegistryAutostart.TrayArgument] : []);
        }
    }

    /// <summary>
    /// Velopack's downloads through the app's one HttpClient, with the upload token. The token goes only to the feed on
    /// the configured site (CLAUDE.md rule 5); the feed is only used when its signature (<c>releases.win.json.sig</c>) holds.
    /// </summary>
    internal sealed class BearerDownloader(HttpClient http, ITokenStore tokens, Uri feed, ECDsa feedKey) : IFileDownloader
    {
        public const string FeedFile = "releases.win.json";

        public async Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = default)
        {
            using var response = await SendAsync(url, cancelToken).ConfigureAwait(false);
            long? total = response.Content.Headers.ContentLength;
            await using var body = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
            await using var file = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            byte[] buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await body.ReadAsync(buffer, cancelToken).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancelToken).ConfigureAwait(false);
                done += read;
                if (total > 0)
                {
                    progress((int)(done * 100 / total.Value));
                }
            }
        }

        public async Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
        {
            byte[] bytes = await GetBytesAsync(url).ConfigureAwait(false);
            await VerifyIfFeedAsync(url, bytes).ConfigureAwait(false);
            return bytes;
        }

        public async Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30) =>
            Encoding.UTF8.GetString(await DownloadBytes(url, headers, timeout).ConfigureAwait(false));

        internal HttpRequestMessage Request(string url)
        {
            var uri = new Uri(url);
            if (!feed.IsBaseOf(uri))
            {
                throw new InvalidOperationException("Update download outside the site's feed.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (tokens.Load() is { } token)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Reveal());
            }

            return request;
        }

        internal bool IsSigned(byte[] feedBytes, byte[] signature) =>
            feedKey.VerifyData(feedBytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        // The feed names every package with its size and SHA256, so a signed feed covers the packages too.
        private async Task VerifyIfFeedAsync(string url, byte[] bytes)
        {
            string path = new Uri(url).AbsolutePath;
            if (!path.EndsWith("/" + FeedFile, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string signatureUrl = new UriBuilder(url) { Query = "" }.Uri.AbsoluteUri + ".sig";
            byte[] signature;
            try
            {
                signature = await GetBytesAsync(signatureUrl).ConfigureAwait(false);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidDataException("The update feed has no signature.", e);
            }

            if (!IsSigned(bytes, signature))
            {
                throw new InvalidDataException("The update feed's signature doesn't hold.");
            }
        }

        private async Task<byte[]> GetBytesAsync(string url)
        {
            using var response = await SendAsync(url, CancellationToken.None).ConfigureAwait(false);
            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
        {
            using var request = Request(url);
            // Headers only: a large package may take longer than the client's timeout to arrive.
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            try
            {
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
    }
}
