using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// A run of characters on a page that matches the search query.
/// </summary>
public readonly struct PdfPanelSearchMatch
{
    /// <summary>
    /// Initializes a match covering <paramref name="range"/>, whose characters span <paramref name="bounds"/>.
    /// </summary>
    public PdfPanelSearchMatch(in PdfPanelTextRange range, in PdfRectangle bounds)
    {
        Range = range;
        Bounds = bounds;
    }

    /// <summary>
    /// Matched characters.
    /// </summary>
    public PdfPanelTextRange Range { get; }

    /// <summary>
    /// Area the matched characters cover, in unscaled page space.
    /// </summary>
    public PdfRectangle Bounds { get; }
}
