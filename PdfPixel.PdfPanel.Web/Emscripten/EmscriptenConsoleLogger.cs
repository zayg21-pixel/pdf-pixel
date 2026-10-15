using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Emscripten;

/// <summary>
/// An <see cref="ILogger"/> that writes to the browser console through <see cref="EmscriptenConsoleLoggerFactory"/>.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class EmscriptenConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogLevel _minimumLevel;

    internal EmscriptenConsoleLogger(string categoryName, LogLevel minimumLevel)
    {
        _categoryName = categoryName;
        _minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _minimumLevel;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (exception != null)
        {
            message = $"{message}{Environment.NewLine}{exception}";
        }

        var prefix = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        EmscriptenConsoleLoggerFactory.Write($"[{prefix}] {_categoryName}: {message}", logLevel);
    }

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
        NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}
