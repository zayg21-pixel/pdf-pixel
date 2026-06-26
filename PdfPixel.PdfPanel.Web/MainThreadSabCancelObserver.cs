using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Client-side observer that cancels worker rendering by setting SAB flags from the main thread.
/// <see cref="Notify"/> is a no-op because the client never executes commands.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class MainThreadSabCancelObserver : IPdfCancellableExecutionObserver
{
    private readonly string _containerId;
    private readonly string _requestId;
    private readonly int _flagType;

    internal MainThreadSabCancelObserver(string containerId, string requestId, CancelFlagType flagType)
    {
        _containerId = containerId;
        _requestId = requestId;
        _flagType = (int)flagType;
    }

    /// <inheritdoc />
    public void Notify()
    {
    }

    /// <inheritdoc />
    public void Cancel() => EmscriptenInterop.SetMainRequestFlag(_containerId, _requestId, _flagType, 1);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
