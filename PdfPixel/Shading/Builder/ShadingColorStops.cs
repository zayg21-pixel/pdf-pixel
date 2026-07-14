using SkiaSharp;

namespace PdfPixel.Shading;

/// <summary>
/// Holds the color and position arrays produced by evaluating a shading's
/// functions across its domain range.
/// </summary>
public sealed class ShadingColorStops
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShadingColorStops"/> class.
    /// </summary>
    /// <param name="colors">Color stops.</param>
    /// <param name="positions">Gradient positions.</param>
    public ShadingColorStops(SKColor[] colors, float[] positions)
    {
        Colors = colors;
        Positions = positions;
    }

    /// <summary>
    /// Gets the color stops.
    /// </summary>
    public SKColor[] Colors { get; }

    /// <summary>
    /// Gets the gradient positions.
    /// </summary>
    public float[] Positions { get; }
}
