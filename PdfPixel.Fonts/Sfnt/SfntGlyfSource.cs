using PdfPixel.Fonts.Typeface;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// The source a "glyf" table's bytes are read from: the font's stream, together with the "glyf"
/// table's byte range within it. Combined with a glyph's <c>loca</c> offset, this locates any single
/// glyph's raw bytes without ever reading the rest of the table.
/// </summary>
public readonly struct SfntGlyfSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyfSource"/> struct.
    /// </summary>
    /// <param name="stream">The font's source stream.</param>
    /// <param name="glyfRecord">The "glyf" table's byte range within <paramref name="stream"/>.</param>
    public SfntGlyfSource(ReadOnlyFontStream stream, in SfntTableRecord glyfRecord)
    {
        Stream = stream;
        GlyfRecord = glyfRecord;
    }

    /// <summary>
    /// Gets the font's source stream.
    /// </summary>
    public ReadOnlyFontStream Stream { get; }

    /// <summary>
    /// Gets the "glyf" table's byte range within <see cref="Stream"/>.
    /// </summary>
    public SfntTableRecord GlyfRecord { get; }
}
