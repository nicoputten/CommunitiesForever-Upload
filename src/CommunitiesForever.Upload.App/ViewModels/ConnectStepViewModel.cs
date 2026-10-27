using CommunitiesForever.Upload.Core.Site;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

public enum ConnectState
{
    Starting,
    Waiting,
    Expired,
    Denied,
    Unreachable,
    Failed,
    Connected,
}

/// <summary>Setup step 1 (board <c>setup</c>): connect in the browser, or paste a token.</summary>
public sealed partial class ConnectStepViewModel : ObservableObject, IDisposable
{
    private readonly ConnectFlow _flow;
    private CancellationTokenSource? _running;

    public ConnectStepViewModel(ConnectFlow flow, SiteOptions site)
    {
        _flow = flow;
        SiteHost = site.BaseUri.Host;
    }

    /// <summary>Raised once the token is stored and accepted.</summary>
    public event EventHandler<SiteIdentity>? Connected;

    public IUiServices? Ui { get; set; }

    public string SiteHost { get; }

    public string Heading => $"Connect to {SiteHost}";

    // The explainer after the highlighted file name (setup board).
    public string ExplainerEnd => $" to {SiteHost} after you play. It only gets permission to upload; it can't see or change anything else.";

    public string BrowserOpened => $"Your browser opened {SiteHost}. Check that it shows this code:";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaiting), nameof(HasProblem), nameof(Problem), nameof(IsStarting), nameof(IsConnected))]
    public partial ConnectState State { get; set; } = ConnectState.Starting;

    [ObservableProperty]
    public partial string UserCode { get; set; } = "";

    /// <summary>"Connected as Nico#2201", once connected.</summary>
    [ObservableProperty]
    public partial string ConnectedText { get; set; } = "";

    [ObservableProperty]
    public partial bool LinkCopied { get; set; }

    [ObservableProperty]
    public partial bool IsPasteOpen { get; set; }

    [ObservableProperty]
    public partial string PasteText { get; set; } = "";

    [ObservableProperty]
    public partial string? PasteError { get; set; }

    [ObservableProperty]
    public partial bool IsPasting { get; set; }

    public bool IsStarting => State == ConnectState.Starting;

    public bool IsWaiting => State == ConnectState.Waiting;

    public bool IsConnected => State == ConnectState.Connected;

    public bool HasProblem => State is ConnectState.Expired or ConnectState.Denied or ConnectState.Unreachable or ConnectState.Failed;

    // Wording follows the states board.
    public string Problem => State switch
    {
        ConnectState.Expired => "The code expired before it was approved.",
        ConnectState.Denied => "It was denied on the site.",
        ConnectState.Unreachable => $"Can't reach {SiteHost}.",
        ConnectState.Failed => "Connecting didn't work.",
        _ => "",
    };

    private Uri? Link { get; set; }

    /// <summary>Requests a code, opens the browser and waits for the approval. Called when step 1 opens, and by "Try again".</summary>
    [RelayCommand]
    public async Task StartAsync()
    {
        Cancel();
        var running = _running = new CancellationTokenSource();
        State = ConnectState.Starting;
        LinkCopied = false;
        var progress = new Progress<ConnectProgress>(p =>
        {
            if (p is ConnectProgress.WaitingForBrowser waiting && !running.IsCancellationRequested)
            {
                UserCode = waiting.UserCode;
                State = ConnectState.Waiting;
            }
        });

        try
        {
            var outcome = await _flow.ConnectAsync(new LinkCatcher(progress, uri => Link = uri), running.Token);
            Finish(outcome);
        }
        catch (OperationCanceledException) when (running.IsCancellationRequested)
        {
            // Cancelled by "Try again", pasting a token, or closing the window.
        }
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (Link is { } link && Ui is { } ui)
        {
            await ui.SetClipboardTextAsync(link.AbsoluteUri);
            LinkCopied = true;
        }
    }

    [RelayCommand]
    private void TogglePaste() => IsPasteOpen = !IsPasteOpen;

    [RelayCommand]
    private async Task UsePastedTokenAsync()
    {
        IsPasting = true;
        PasteError = null;
        try
        {
            var outcome = await _flow.PasteTokenAsync(PasteText);
            PasteError = outcome switch
            {
                ConnectOutcome.MalformedToken => "That isn't an upload token. It starts with cfu_.",
                ConnectOutcome.UnknownToken => "The site doesn't know this token. Create a new one on the uploader page.",
                ConnectOutcome.Failed { Failure.Kind: SiteError.Offline } => $"Can't reach {SiteHost}.",
                ConnectOutcome.Failed => "The token couldn't be checked. Try again.",
                _ => null,
            };
            if (outcome is ConnectOutcome.Connected)
            {
                Cancel();
                Finish(outcome);
            }
        }
        finally
        {
            IsPasting = false;
        }
    }

    public void Dispose() => Cancel();

    private void Finish(ConnectOutcome outcome)
    {
        switch (outcome)
        {
            case ConnectOutcome.Connected connected:
                ConnectedText = $"Connected as {connected.Identity.BattleTag}";
                State = ConnectState.Connected;
                Connected?.Invoke(this, connected.Identity);
                break;
            case ConnectOutcome.Denied:
                State = ConnectState.Denied;
                break;
            case ConnectOutcome.Expired:
                State = ConnectState.Expired;
                break;
            case ConnectOutcome.Failed { Failure.Kind: SiteError.Offline or SiteError.ServerError }:
                State = ConnectState.Unreachable;
                break;
            default:
                State = ConnectState.Failed;
                break;
        }
    }

    private void Cancel()
    {
        if (_running is { } running)
        {
            _running = null;
            running.Cancel();
            running.Dispose();
        }
    }

    // Keeps the opened page for "Copy the link".
    private sealed class LinkCatcher(IProgress<ConnectProgress> inner, Action<Uri> link) : IProgress<ConnectProgress>
    {
        public void Report(ConnectProgress value)
        {
            if (value is ConnectProgress.WaitingForBrowser waiting)
            {
                link(waiting.Page);
            }

            inner.Report(value);
        }
    }
}
