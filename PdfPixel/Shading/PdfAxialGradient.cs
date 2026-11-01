using PdfPixel.Color;
using PdfPixel.Geometry;

namespace PdfPixel.Shading;

/// <summary>
/// An axial (Type 2) gradient: a linear ramp of colors between two points.
/// </summary>
public sealed class PdfAxialGradient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAxialGradient"/> class.
    /// </summary>
    /// <param name="start">Gradient start point.</param>
    /// <param name="end">Gradient end point.</param>
    /// <param name="colors">Color stops, one per entry in <paramref name="positions"/>.</param>
    /// <param name="positions">Gradient positions in the range [0, 1].</param>
    /// <param name="extendStart">Whether the gradient extends beyond its start point.</param>
    /// <param name="extendEnd">Whether the gradient extends beyond its end point.</param>
    internal PdfAxialGradient(in PdfPoint start, in PdfPoint end, PdfColor[] colors, float[] positions, bool extendStart, bool extendEnd)
    {
        Start = start;
        End = end;
        Colors = colors;
        Positions = positions;
        ExtendStart = extendStart;
        ExtendEnd = extendEnd;
    }

    /// <summary>
    /// Gets the gradient start point.
    /// </summary>
    public PdfPoint Start { get; }

    /// <summary>
    /// Gets the gradient end point.
    /// </summary>
    public PdfPoint End { get; }

    /// <summary>
    /// Gets the color stops, one per entry in <see cref="Positions"/>, in the color space of the shading content.
    /// </summary>
    public PdfColor[] Colors { get; }

    /// <summary>
    /// Gets the gradient positions in the range [0, 1].
    /// </summary>
    public float[] Positions { get; }

    /// <summary>
    /// Gets whether the gradient extends beyond its start point.
    /// </summary>
    public bool ExtendStart { get; }

    /// <summary>
    /// Gets whether the gradient extends beyond its end point.
    /// </summary>
    public bool ExtendEnd { get; }
}
