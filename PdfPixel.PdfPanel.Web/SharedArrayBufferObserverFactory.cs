using PdfPixel.PdfPanel.ContentProvider;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// <see cref="IPdfExecutionObserverFactory"/> that creates
/// <see cref="SharedArrayBufferExecutionObserver"/> instances keyed by the current request ID.
/// Call <see cref="SetCurrentRequestId"/> before each <c>UpdateContent</c> invocation.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class SharedArrayBufferObserverFactory : IPdfExecutionObserverFactory
{
    private string _currentRequestId = string.Empty;

    internal void SetCurrentRequestId(string requestId) => _currentRequestId = requestId;

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber)
        => new SharedArrayBufferExecutionObserver(_currentRequestId, CancelFlagType.Parse);

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber)
        => new SharedArrayBufferExecutionObserver(_currentRequestId, CancelFlagType.Content);
}
