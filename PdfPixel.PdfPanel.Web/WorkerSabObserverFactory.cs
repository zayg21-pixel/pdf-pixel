using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Worker-side <see cref="IPdfExecutionObserverFactory"/> that owns SAB cleanup
/// on the worker thread. Created observers read SAB flags atomically to detect cancellation.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class WorkerSabObserverFactory : IPdfExecutionObserverFactory
{
    private string _currentRequestId = string.Empty;

    internal void SetCurrentRequestId(string requestId) => _currentRequestId = requestId;

    internal void FreeRequest(string requestId) => EmscriptenInterop.FreeWorkerRequestSab(requestId);

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber)
        => new WorkerSabExecutionObserver(_currentRequestId, CancelFlagType.Parse);

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber)
        => new WorkerSabExecutionObserver(_currentRequestId, CancelFlagType.Content);
}
