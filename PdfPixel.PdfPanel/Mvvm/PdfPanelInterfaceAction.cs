namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Defines actions that can be performed on the PDF panel.
/// </summary>
public enum PdfPanelInterfaceAction
{
    /// <summary>
    /// Increase zoom level.
    /// </summary>
    ZoomIn,

    /// <summary>
    /// Decrease zoom level.
    /// </summary>
    ZoomOut,

    /// <summary>
    /// Select the next search result.
    /// </summary>
    NextSearchResult,

    /// <summary>
    /// Select the previous search result.
    /// </summary>
    PreviousSearchResult,

    /// <summary>
    /// Request panel redraw.
    /// </summary>
    RequestRedraw,

    /// <summary>
    /// Request the panel to be drawn again from the content already produced, running its drawing callbacks.
    /// </summary>
    RequestPresent,

    /// <summary>
    /// Scroll to a <see cref="Navigation.PdfDestination"/>.
    /// </summary>
    ScrollToDestination
}
