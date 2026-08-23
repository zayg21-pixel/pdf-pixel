namespace PdfPixel.ResourceGenerator.Fonts;

/// <summary>
/// One entry of an AFM file's CharMetrics section, in glyph space units (1/1000 em).
/// </summary>
internal readonly struct AfmCharacterMetric
{
    public AfmCharacterMetric(int code, string name, int width)
    {
        Code = code;
        Name = name;
        Width = width;
    }

    /// <summary>
    /// The character code in the font's built-in encoding, or -1 when the entry carries none.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// The glyph name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The advance width.
    /// </summary>
    public int Width { get; }
}
