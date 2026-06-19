using Microsoft.Extensions.Logging;
using System.Text;

namespace ActivityTracker.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new object();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_filePath, _lock, categoryName);
    }

    public void Dispose()
    {
    }

    private class FileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly object _lock;
        private readonly string _category;

        public FileLogger(string filePath, object @lock, string category)
        {
            _filePath = filePath;
            _lock = @lock;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                var message = formatter(state, exception);
                var line = new StringBuilder()
                    .Append(DateTime.UtcNow.ToString("o"))
                    .Append(' ')
                    .Append(logLevel.ToString())
                    .Append(' ')
                    .Append(_category)
                    .Append(' ')
                    .Append(message);

                if (exception != null)
                {
                    line.Append(' ').Append(exception);
                }

                lock (_lock)
                {
                    File.AppendAllText(_filePath, line.ToString() + Environment.NewLine);
                }
            }
            catch
            {
                // Swallow logging failures to avoid crashing the app
            }
        }
    }
}
