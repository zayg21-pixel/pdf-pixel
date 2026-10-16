using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;

namespace PdfPixel.Demo.Wpf.Logging;

/// <summary>
/// Logger that adds its log messages to an observable collection.
/// </summary>
internal class ObservableLogger : ILogger
{
    private readonly ObservableCollection<LogMessage> _logMessages;
    private readonly string _categoryName;
    private readonly object _logMessagesLock;

    public ObservableLogger(ObservableCollection<LogMessage> logMessages, string categoryName, object logMessagesLock)
    {
        _logMessages = logMessages;
        _categoryName = categoryName;
        _logMessagesLock = logMessagesLock;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        string message = formatter(state, exception);

        if (exception != null)
        {
            message = message + Environment.NewLine + exception;
        }

        var logMessage = new LogMessage
        {
            DateTime = System.DateTime.Now,
            LogLevel = logLevel,
            CategoryName = _categoryName,
            FormattedMessage = message
        };

        lock (_logMessagesLock)
        {
            _logMessages.Add(logMessage);
        }
    }
}
