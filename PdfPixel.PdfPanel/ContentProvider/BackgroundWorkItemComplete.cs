using PdfPixel.PdfPanel.Execution;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Completion of a background work item for a page.
/// </summary>
internal sealed class BackgroundWorkItemComplete
{
    /// <summary>
    /// Initializes the completion for the given 1-based page number, result and observer of the work item.
    /// </summary>
    public BackgroundWorkItemComplete(int pageNumber, BackgroundWorkItemResult result, IPdfCancellableExecutionObserver observer)
    {
        PageNumber = pageNumber;
        Result = result;
        Observer = observer;
    }

    /// <summary>
    /// 1-based page number the work item processed.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// How the work item finished.
    /// </summary>
    public BackgroundWorkItemResult Result { get; }

    /// <summary>
    /// Observer the work item ran with.
    /// </summary>
    public IPdfCancellableExecutionObserver Observer { get; }
}
