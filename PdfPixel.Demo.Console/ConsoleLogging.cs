using Microsoft.Extensions.Logging;

namespace PdfPixel.Console.Demo
{
    /// <summary>
    /// Provides a simple console logger provider implementation with configurable minimum level.
    /// Ensures trace/debug messages are not filtered out by global logging filters when requested.
    /// </summary>
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

                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                System.Console.WriteLine($"{timestamp} [{logLevel}] {_category}: {message}");
                if (exception != null)
                {
                    System.Console.WriteLine(exception.ToString());
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();
                public void Dispose()
                {
                }
            }
        }
    }

    /// <summary>
    /// Logging builder extensions for registering the simple console logger provider.
    /// Sets the global minimum level so that requested lower levels (Trace/Debug) are not filtered out
    /// by the logging pipeline before reaching the provider.
    /// </summary>
    internal static class LoggingBuilderExtensions
    {
        /// <summary>
        /// Adds the <see cref="SimpleConsoleLoggerProvider"/> to the logging builder with the specified minimum level.
        /// Ensures <paramref name="minLevel"/> is also applied as the global filter.
        /// </summary>
        /// <param name="builder">The logging builder to configure.</param>
        /// <param name="minLevel">Minimum log level to emit (default Trace).</param>
        /// <returns>The same <see cref="ILoggingBuilder"/> for chaining.</returns>
        public static ILoggingBuilder AddConsole(this ILoggingBuilder builder, LogLevel minLevel = LogLevel.Trace)
        {
            builder.SetMinimumLevel(minLevel);
            builder.AddProvider(new SimpleConsoleLoggerProvider(minLevel));
            return builder;
        }
    }
}
