using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PdfPixel.Demo.Wpf
{
    /// <summary>
    /// Represents a log message with details for display or analysis.
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// Gets or sets the date and time of the log message.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the logger category name.
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// Gets or sets the formatted log message.
        /// </summary>
        public string FormattedMessage { get; set; }
    }

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

        private class ObservableLogger : ILogger
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

                var logMessage = new LogMessage
                {
                    DateTime = System.DateTime.Now,
                    LogLevel = logLevel,
                    CategoryName = _categoryName,
                    FormattedMessage = formatter(state, exception)
                };

                lock (_logMessagesLock)
                {
                    _logMessages.Add(logMessage);
                }
            }
        }
    }
}
