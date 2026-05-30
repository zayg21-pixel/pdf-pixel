namespace PdfPixel.PdfPanel;

/// <summary>
/// Controls how the panel automatically adjusts the zoom scale to fit the viewport.
/// </summary>
public enum PdfPanelAutoScaleMode
{
    /// <summary>
    /// No automatic scaling.
    /// </summary>
    NoAutoScale,

    /// <summary>
    /// Scale to pages width.
    /// </summary>
    ScaleToWidth,

    /// <summary>
    /// Scale to height of pages.
    /// </summary>
    ScaleToHeight
}
