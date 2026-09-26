namespace PdfPixel.PdfPanel.Execution;

/// <summary>
/// Creates the <see cref="IPdfCancellableExecutionObserver"/> instances that
/// <see cref="ContentProvider.PdfPageContentProvider"/> runs page work with.
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
