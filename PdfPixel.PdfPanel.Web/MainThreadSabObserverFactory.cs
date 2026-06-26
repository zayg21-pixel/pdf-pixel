using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Client-side <see cref="IPdfExecutionObserverFactory"/> that owns SAB allocation and cleanup
/// on the main thread. Created observers cancel worker rendering by setting SAB flags atomically.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class MainThreadSabObserverFactory : IPdfExecutionObserverFactory
{
    private readonly string _containerId;
    private string _currentRequestId = string.Empty;

    internal MainThreadSabObserverFactory(string containerId)
    {
        _containerId = containerId;
    }

    internal string AllocateRequest()
    {
        string requestId = System.Guid.NewGuid().ToString();
        EmscriptenInterop.AllocRequestSab(_containerId, requestId);
        _currentRequestId = requestId;
        return requestId;
    }

    internal void FreeRequest(string requestId) => EmscriptenInterop.FreeMainRequestSab(_containerId, requestId);

    /// <inheritdoc />
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber)
        => new MainThreadSabCancelObserver(_containerId, _currentRequestId, CancelFlagType.Parse);

    /// <inheritdoc />
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber)
        => new MainThreadSabCancelObserver(_containerId, _currentRequestId, CancelFlagType.Content);
}
