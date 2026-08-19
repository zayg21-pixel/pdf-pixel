namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="IPdfExecutionObserverFactory"/> that creates
/// <see cref="PdfYieldingExecutionObserver"/> instances.
/// </summary>
public sealed class PdfYieldingObserverFactory : IPdfExecutionObserverFactory
{
    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber) => new PdfYieldingExecutionObserver();

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber) => new PdfYieldingExecutionObserver();
}
