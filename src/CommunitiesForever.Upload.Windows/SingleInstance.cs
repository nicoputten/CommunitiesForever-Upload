using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// One app per Windows user: a named mutex decides who runs; a second start hands its arguments (an app link, or
/// nothing for "show") to the running app through a named pipe and quits. Disposing stops listening (CLAUDE.md rule 11).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private Task _listening = Task.CompletedTask;

    public SingleInstance(string name = AppIdentity.AppId)
    {
        string user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _pipeName = $"{name}.{user}";
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.{user}", out bool createdNew);
        IsFirst = createdNew;
    }

    /// <summary>Raised on a background thread with a later start's arguments.</summary>
    public event EventHandler<string[]>? ArgumentsReceived;

    public bool IsFirst { get; }

    public void Listen() => _listening = Task.Run(ListenAsync);

    /// <summary>From a second start: passes the arguments on. False when the running app didn't answer.</summary>
    public bool Forward(string[] args)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            pipe.Connect(TimeSpan.FromSeconds(3));
            pipe.Write(Encoding.UTF8.GetBytes(string.Join('\n', args)));
            return true;
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _listening.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Stopped.
        }

        _stop.Dispose();
        if (IsFirst)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Only the owning thread may release it; closing the handle below frees it all the same.
            }
        }

        _mutex.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await using (pipe.ConfigureAwait(false))
                {
                    await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    string text = await reader.ReadToEndAsync(_stop.Token).ConfigureAwait(false);
                    ArgumentsReceived?.Invoke(this, text.Length == 0 ? [] : text.Split('\n'));
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A start that gave up halfway; listen for the next one.
            }
        }
    }
}
