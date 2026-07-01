namespace PdfPixel.ResourceGenerator.Metrics;

/// <summary>
/// Ascent/descent/cap-height/x-height for a Standard 14 font variant, in glyph space units (1/1000 em).
/// </summary>
internal readonly struct Standard14BasicMetrics
{
    public Standard14BasicMetrics(float? ascent, float? descent, float? capHeight, float? xHeight)
    {
        Ascent = ascent;
        Descent = descent;
        CapHeight = capHeight;
        XHeight = xHeight;
    }

    public float? Ascent { get; }

    public float? Descent { get; }

    public float? CapHeight { get; }

    public float? XHeight { get; }
}
