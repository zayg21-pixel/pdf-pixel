using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Emscripten;

/// <summary>
/// An <see cref="ILoggerFactory"/> that writes to the browser console via
/// Emscripten's <c>emscripten_console_log/warn/error</c> functions.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class EmscriptenConsoleLoggerFactory : ILoggerFactory
{
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Initialises the factory with the given minimum log level.
    /// </summary>
    /// <param name="minimumLevel">Messages below this level are suppressed.</param>
    public EmscriptenConsoleLoggerFactory(LogLevel minimumLevel = LogLevel.Debug) => _minimumLevel = minimumLevel;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => new EmscriptenConsoleLogger(categoryName, _minimumLevel);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
    }

    // P/Invoke into dotnet_console_log in emscripten.c.
    [DllImport("emscripten", EntryPoint = "dotnet_console_log")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern void NativeConsoleLog(string message, int logLevel);

    internal static void Write(string message, LogLevel level)
        => NativeConsoleLog(message, (int)level);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
