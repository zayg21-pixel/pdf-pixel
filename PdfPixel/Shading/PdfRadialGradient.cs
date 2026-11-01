using PdfPixel.Color;
using PdfPixel.Geometry;

namespace PdfPixel.Shading;

/// <summary>
/// A radial (Type 3) gradient: a ramp of colors between two circles.
/// </summary>
public sealed class PdfRadialGradient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRadialGradient"/> class.
    /// </summary>
    /// <param name="center0">Center of the start circle.</param>
    /// <param name="radius0">Radius of the start circle.</param>
    /// <param name="center1">Center of the end circle.</param>
    /// <param name="radius1">Radius of the end circle.</param>
    /// <param name="colors">Color stops, one per entry in <paramref name="positions"/>.</param>
    /// <param name="positions">Gradient positions in the range [0, 1].</param>
    /// <param name="extendStart">Whether the gradient extends beyond its start circle.</param>
    /// <param name="extendEnd">Whether the gradient extends beyond its end circle.</param>
    internal PdfRadialGradient(
        in PdfPoint center0,
        float radius0,
        in PdfPoint center1,
        float radius1,
        PdfColor[] colors,
        float[] positions,
        bool extendStart,
        bool extendEnd)
    {
        Center0 = center0;
        Radius0 = radius0;
        Center1 = center1;
        Radius1 = radius1;
        Colors = colors;
        Positions = positions;
        ExtendStart = extendStart;
        ExtendEnd = extendEnd;
    }

    /// <summary>
    /// Gets the center of the start circle.
    /// </summary>
    public PdfPoint Center0 { get; }

    /// <summary>
    /// Gets the radius of the start circle.
    /// </summary>
    public float Radius0 { get; }

    /// <summary>
    /// Gets the center of the end circle.
    /// </summary>
    public PdfPoint Center1 { get; }

    /// <summary>
    /// Gets the radius of the end circle.
    /// </summary>
    public float Radius1 { get; }

    /// <summary>
    /// Gets the color stops, one per entry in <see cref="Positions"/>, in the color space of the shading content.
    /// </summary>
    public PdfColor[] Colors { get; }

    /// <summary>
    /// Gets the gradient positions in the range [0, 1].
    /// </summary>
    public float[] Positions { get; }

    /// <summary>
    /// Gets whether the gradient extends beyond its start circle.
    /// </summary>
    public bool ExtendStart { get; }

    /// <summary>
    /// Gets whether the gradient extends beyond its end circle.
    /// </summary>
    public bool ExtendEnd { get; }
}
