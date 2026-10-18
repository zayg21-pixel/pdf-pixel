namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Indicates how a background work item finished.
/// </summary>
internal enum BackgroundWorkItemResult
{
    /// <summary>
    /// The work item completed its work.
    /// </summary>
    Success,

    /// <summary>
    /// The work item was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The work item failed with an error.
    /// </summary>
    Error
}
