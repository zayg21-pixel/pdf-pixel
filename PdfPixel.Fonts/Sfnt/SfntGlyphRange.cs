namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single glyph's byte range within the "glyf" table, as resolved from "loca". A zero
/// <see cref="Length"/> means the glyph has no outline (e.g. space).
/// </summary>
public readonly struct SfntGlyphRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyphRange"/> struct.
    /// </summary>
    /// <param name="offset">The glyph's byte offset into "glyf".</param>
    /// <param name="length">The glyph's byte length within "glyf".</param>
    public SfntGlyphRange(uint offset, uint length)
    {
        Offset = offset;
        Length = length;
    }

    /// <summary>
    /// Gets the glyph's byte offset into "glyf".
    /// </summary>
    public uint Offset { get; }

    /// <summary>
    /// Gets the glyph's byte length within "glyf".
    /// </summary>
    public uint Length { get; }
}
