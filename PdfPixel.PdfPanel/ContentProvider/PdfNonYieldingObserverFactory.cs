namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="IPdfExecutionObserverFactory"/> that creates
/// <see cref="PdfNonYieldingExecutionObserver"/> instances.
/// </summary>
public sealed class PdfNonYieldingObserverFactory : IPdfExecutionObserverFactory
{
    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber) => new PdfNonYieldingExecutionObserver();

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber) => new PdfNonYieldingExecutionObserver();
}
