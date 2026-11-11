using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Rendering;

namespace PdfPixel.Pattern;

/// <summary>
/// Base class for all PDF pattern types (/Pattern). Provides common metadata shared by
/// tiling and shading patterns.
/// </summary>
public abstract class PdfPattern
{
    /// <summary>
    /// Initializes the pattern with its transformation matrix and pattern type.
    /// </summary>
    protected PdfPattern(in PdfMatrix matrix, PdfPatternType patternType)
    {
        Matrix = matrix;
        PatternType = patternType;
    }

    /// <summary>
    /// Pattern transformation matrix (identity if /Matrix absent).
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Underlying pattern type enum.
    /// </summary>
    public PdfPatternType PatternType { get; }

    /// <summary>
    /// Returns the group an object painted with the pattern takes as its source (ISO 32000-2 11.6.7), or
    /// <see langword="null"/> when the pattern paints nothing.
    /// </summary>
    /// <param name="state">Graphics state the object is painted in.</param>
    /// <param name="paint">The paint the object is painted with.</param>
    internal abstract PdfGroupSource? GetGroupSource(PdfGraphicsState state, PdfPaint paint);
}
