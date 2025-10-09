using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PdfReadTests
{
    internal sealed class SimpleConsoleLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel _minLevel;

        public SimpleConsoleLoggerProvider(LogLevel minLevel)
        {
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SimpleConsoleLogger(categoryName, _minLevel);
        }

        public void Dispose()
        {
        }

        private sealed class SimpleConsoleLogger : ILogger
        {
            private readonly string _category;
            private readonly LogLevel _minLevel;

            public SimpleConsoleLogger(string category, LogLevel minLevel)
            {
                _category = category;
                _minLevel = minLevel;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= _minLevel;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }
                if (formatter == null)
                {
                    return;
                }
                string message = formatter(state, exception);
                if (string.IsNullOrEmpty(message) && exception == null)
                {
                    return;
                }
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"{timestamp} [{logLevel}] {_category}: {message}");
                if (exception != null)
                {
                    Console.WriteLine(exception.ToString());
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();
                public void Dispose() { }
            }
        }
    }

    internal static class LoggingBuilderExtensions
    {
        public static ILoggingBuilder AddConsole(this ILoggingBuilder builder, LogLevel minLevel = LogLevel.Debug)
        {
            builder.AddProvider(new SimpleConsoleLoggerProvider(minLevel));
            return builder;
        }
    }
}
