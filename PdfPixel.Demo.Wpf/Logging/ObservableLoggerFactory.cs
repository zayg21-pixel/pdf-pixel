using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;

namespace PdfPixel.Demo.Wpf.Logging;

/// <summary>
/// Logger factory that writes log messages to an observable collection.
/// </summary>
public class ObservableLoggerFactory : ILoggerFactory
{
    private readonly object _logMessagesLock;

    public ObservableLoggerFactory(ObservableCollection<LogMessage> logMessages, object logMessagesLock)
    {
        LogMessages = logMessages ?? throw new ArgumentNullException(nameof(logMessages));
        _logMessagesLock = logMessagesLock ?? throw new ArgumentNullException(nameof(logMessagesLock));
    }

    public ObservableCollection<LogMessage> LogMessages { get; }

    public void AddProvider(ILoggerProvider provider)
    {
        // Not needed for this simple implementation.
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ObservableLogger(LogMessages, categoryName, _logMessagesLock);
    }

    public void Dispose()
    {
        // Nothing to dispose.
    }
}
