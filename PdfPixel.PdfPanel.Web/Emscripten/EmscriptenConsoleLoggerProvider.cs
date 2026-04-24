using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Emscripten;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes to the browser console via
/// Emscripten's <c>emscripten_console_log/warn/error</c> functions.
/// Safe to call from any thread, including WASM worker threads.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class EmscriptenConsoleLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Initialises the provider with the given minimum log level.
    /// </summary>
    /// <param name="minimumLevel">Messages below this level are suppressed.</param>
    public EmscriptenConsoleLoggerProvider(LogLevel minimumLevel = LogLevel.Debug)
    {
        _minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        new EmscriptenConsoleLogger(categoryName, _minimumLevel);

    /// <inheritdoc />
    public void Dispose() { }

    // P/Invoke into dotnet_console_log in emscripten.c.
    // emscripten_console_* functions do not require synchronisation with the
    // browser main thread and are safe to call from any pthread.
    [DllImport("emscripten", EntryPoint = "dotnet_console_log")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern void NativeConsoleLog(string message, int logLevel);

    internal static void Write(string message, LogLevel level) =>
        NativeConsoleLog(message, (int)level);
}

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

        EmscriptenConsoleLoggerProvider.Write($"[{prefix}] {_categoryName}: {message}", logLevel);
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

/// <summary>
/// Extension methods for registering <see cref="EmscriptenConsoleLoggerProvider"/>
/// with <see cref="ILoggingBuilder"/>.
/// </summary>
[SupportedOSPlatform("browser")]
public static class EmscriptenConsoleLoggerExtensions
{
    /// <summary>
    /// Adds the Emscripten browser-console logger to the logging pipeline.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <param name="minimumLevel">Minimum level to emit. Defaults to <see cref="LogLevel.Debug"/>.</param>
    public static ILoggingBuilder AddEmscriptenConsole(
        this ILoggingBuilder builder,
        LogLevel minimumLevel = LogLevel.Debug)
    {
        builder.AddProvider(new EmscriptenConsoleLoggerProvider(minimumLevel));
        return builder;
    }
}
