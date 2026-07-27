using PdfPixel.Color;
using PdfPixel.Geometry;

namespace PdfPixel.Shading.Model;

/// <summary>
/// An axial (Type 2) gradient: a linear ramp of colors between two points.
/// </summary>
public sealed class PdfLinearGradient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLinearGradient"/> class.
    /// </summary>
    /// <param name="start">Gradient start point.</param>
    /// <param name="end">Gradient end point.</param>
    /// <param name="colors">Color stops, one per entry in <paramref name="positions"/>.</param>
    /// <param name="positions">Gradient positions in the range [0, 1].</param>
    public PdfLinearGradient(in PdfPoint start, in PdfPoint end, PdfColor[] colors, float[] positions)
    {
        Start = start;
        End = end;
        Colors = colors;
        Positions = positions;
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
    /// Gets the color stops, one per entry in <see cref="Positions"/>.
    /// </summary>
    public PdfColor[] Colors { get; }

    /// <summary>
    /// Gets the gradient positions in the range [0, 1].
    /// </summary>
    public float[] Positions { get; }
}
