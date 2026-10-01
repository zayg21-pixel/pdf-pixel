namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Layers of the user interface graphics of a page, in draw order.
/// </summary>
internal enum PdfPanelGraphicsLayer
{
    /// <summary>
    /// Highlights of the search matches.
    /// </summary>
    SearchMatches,

    /// <summary>
    /// Highlight of the text selection.
    /// </summary>
    Selection
}
