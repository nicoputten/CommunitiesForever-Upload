using System.Globalization;
using Microsoft.Extensions.Logging;

namespace CommunitiesForever.Upload.Windows;

/// <summary>
/// Rolling log files in the app's logs folder: uploader.log, rolled to uploader.1.log … uploader.6.log at 1 MB, so at
/// most 7 MB (CLAUDE.md rule 11). Core never logs tokens or other people's names (rule 4); this only writes what it gets.
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    public const long MaxFileBytes = 1024 * 1024;
    public const int MaxFiles = 7;

    private readonly string _directory;
    private readonly LogLevel _minimum;
    private readonly Lock _lock = new();
    private StreamWriter? _writer;

    public RollingFileLoggerProvider(string directory, LogLevel minimum = LogLevel.Information)
    {
        _directory = directory;
        _minimum = minimum;
    }

    private string Current => Path.Combine(_directory, "uploader.log");

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(string line)
    {
        lock (_lock)
        {
            try
            {
                if (_writer is null)
                {
                    Directory.CreateDirectory(_directory);
                    _writer = new StreamWriter(new FileStream(Current, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
                }

                _writer.WriteLine(line);
                if (_writer.BaseStream.Length >= MaxFileBytes)
                {
                    Roll();
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Logging must never stop the app.
            }
        }
    }

    private void Roll()
    {
        _writer?.Dispose();
        _writer = null;
        File.Delete(Numbered(MaxFiles - 1));
        for (int i = MaxFiles - 2; i >= 1; i--)
        {
            if (File.Exists(Numbered(i)))
            {
                File.Move(Numbered(i), Numbered(i + 1));
            }
        }

        File.Move(Current, Numbered(1));
    }

    private string Numbered(int n) => Path.Combine(_directory, $"uploader.{n}.log");

    private sealed class FileLogger(RollingFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= provider._minimum && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            string name = category[(category.LastIndexOf('.') + 1)..];
            string text = $"{time} {logLevel} {name}: {formatter(state, exception)}";
            provider.Write(exception is null ? text : $"{text}{Environment.NewLine}{exception}");
        }
    }
}
