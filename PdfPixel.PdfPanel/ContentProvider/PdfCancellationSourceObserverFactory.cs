namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Default <see cref="IPdfExecutionObserverFactory"/> that creates
/// <see cref="PdfCancellationSourceExecutionObserver"/> instances.
/// Used on platforms with real thread cancellation (e.g. WPF).
/// </summary>
public sealed class PdfCancellationSourceObserverFactory : IPdfExecutionObserverFactory
{
    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber) => new PdfCancellationSourceExecutionObserver();

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber) => new PdfCancellationSourceExecutionObserver();
}
