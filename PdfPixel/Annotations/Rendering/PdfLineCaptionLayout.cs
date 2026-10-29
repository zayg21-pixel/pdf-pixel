using PdfPixel.Geometry;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// The caption of a line, shaped and placed in the coordinates of the line.
/// </summary>
internal readonly struct PdfLineCaptionLayout
{
    /// <summary>
    /// Initializes the layout of a line caption.
    /// </summary>
    public PdfLineCaptionLayout(ShapedGlyph[] glyphs, in PdfMatrix lineMatrix, in PdfPoint origin, in PdfRectangle area)
    {
        Glyphs = glyphs;
        LineMatrix = lineMatrix;
        Origin = origin;
        Area = area;
    }

    /// <summary>
    /// The shaped glyphs of the caption text.
    /// </summary>
    public ShapedGlyph[] Glyphs { get; }

    /// <summary>
    /// The matrix from the coordinates of the line to the page, as created by
    /// <see cref="PdfAnnotationGeometry.CreateLineMatrix"/>.
    /// </summary>
    public PdfMatrix LineMatrix { get; }

    /// <summary>
    /// The start of the caption baseline.
    /// </summary>
    public PdfPoint Origin { get; }

    /// <summary>
    /// The area the caption covers, which the line is kept clear of.
    /// </summary>
    public PdfRectangle Area { get; }
}
