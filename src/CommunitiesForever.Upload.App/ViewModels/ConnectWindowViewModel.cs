using CommunitiesForever.Upload.Core.Site;
using CommunityToolkit.Mvvm.Input;

namespace CommunitiesForever.Upload.App.ViewModels;

/// <summary>The "Connect" window ("Reconnect" in the main window): setup step 1 on its own; closes once connected.</summary>
public sealed partial class ConnectWindowViewModel : IDisposable
{
    public ConnectWindowViewModel(ConnectFlow flow, SiteOptions options)
    {
        Connect = new ConnectStepViewModel(flow, options);
        Connect.Connected += OnConnected;
    }

    /// <summary>The new token is stored; the app picks it up and closes the window.</summary>
    public event EventHandler? Connected;

    public event EventHandler? Cancelled;

    public ConnectStepViewModel Connect { get; }

    public IUiServices? Ui
    {
        get => Connect.Ui;
        set => Connect.Ui = value;
    }

    public Task StartAsync() => Connect.StartAsync();

    public void Dispose()
    {
        Connect.Connected -= OnConnected;
        Connect.Dispose();
        Connect.Ui = null;
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private void OnConnected(object? sender, SiteIdentity identity) => Connected?.Invoke(this, EventArgs.Empty);
}
