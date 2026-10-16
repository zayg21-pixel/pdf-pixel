using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// A run of characters on a page that matches the search query.
/// </summary>
public readonly struct PdfPanelSearchMatch
{
    private readonly PdfPanelText _text;

    /// <summary>
    /// Initializes a match covering <paramref name="range"/> of <paramref name="text"/>, whose characters span <paramref name="bounds"/>.
    /// </summary>
    public PdfPanelSearchMatch(PdfPanelText text, in PdfPanelTextRange range, in PdfRectangle bounds)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
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

    /// <summary>
    /// Text of the matched characters.
    /// </summary>
    public string Text => _text.GetText(Range);
}
