namespace PdfRender.Fonts.Model;

/// <summary>
/// Vertical metrics for a single CID in vertical writing mode.
/// Stores vertical advance (W1y), vertical origin displacement (V1y), and optional horizontal origin displacement (V1x).
/// </summary>
public readonly struct VerticalMetric
{
    /// <summary>
    /// Initializes a new instance of the VerticalMetric struct.
    /// </summary>
    /// <param name="w1">Vertical advance (W1y) for the glyph.</param>
    /// <param name="v1">Vertical displacement (V1y) to the vertical origin.</param>
    /// <param name="v1x">Optional horizontal displacement (V1x) to the vertical origin.</param>
    public VerticalMetric(float w1, float v1, float? v1x = null)
    {
        W1 = w1;
        V1 = v1;
        V1X = v1x;
    }

    /// <summary>
    /// Vertical advance (W1y) for the glyph.
    /// </summary>
    public float W1 { get; }

    /// <summary>
    /// Vertical displacement to the vertical origin (V1y).
    /// </summary>
    public float V1 { get; }

    /// <summary>
    /// Optional horizontal displacement to the vertical origin (V1x). Null when not provided.
    /// </summary>
    public float? V1X { get; }
}
