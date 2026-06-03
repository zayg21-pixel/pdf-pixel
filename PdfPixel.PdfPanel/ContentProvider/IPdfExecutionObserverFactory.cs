namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Creates <see cref="IPdfCancellableExecutionObserver"/> instances for a page.
/// Injected into <see cref="PdfPageContentProvider"/> so that platform-specific
/// cancellation mechanisms (CancellationToken vs SharedArrayBuffer) can be substituted
/// without changing the provider logic.
/// </summary>
public interface IPdfExecutionObserverFactory
{
    /// <summary>
    /// Creates an observer for the page command recording pass.
    /// </summary>
    IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber);

    /// <summary>
    /// Creates an observer for the content picture rendering pass.
    /// </summary>
    IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber);
}
