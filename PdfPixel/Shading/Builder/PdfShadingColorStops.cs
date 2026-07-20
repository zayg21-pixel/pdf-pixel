using PdfPixel.Color;

namespace PdfPixel.Shading;

/// <summary>
/// Holds the color and position arrays produced by evaluating a shading's
/// functions across its domain range.
/// </summary>
public sealed class PdfShadingColorStops
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingColorStops"/> class.
    /// </summary>
    /// <param name="colors">Color stops.</param>
    /// <param name="positions">Gradient positions.</param>
    public PdfShadingColorStops(PdfColor[] colors, float[] positions)
    {
        Colors = colors;
        Positions = positions;
    }

    /// <summary>
    /// Gets the color stops.
    /// </summary>
    public PdfColor[] Colors { get; }

    /// <summary>
    /// Gets the gradient positions.
    /// </summary>
    public float[] Positions { get; }
}
