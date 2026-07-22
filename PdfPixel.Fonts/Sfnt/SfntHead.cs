namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "head" table.
/// </summary>
public class SfntHead
{
    /// <summary>
    /// Gets or sets the table's version number. 1.0 for every known "head" table.
    /// </summary>
    public float Version { get; set; }

    /// <summary>
    /// Gets or sets the font revision number set by the font manufacturer.
    /// </summary>
    public float FontRevision { get; set; }

    /// <summary>
    /// Gets or sets the head table's flags bit field.
    /// </summary>
    public ushort Flags { get; set; }

    /// <summary>
    /// Gets or sets the size of the em square in font design units.
    /// </summary>
    public ushort UnitsPerEm { get; set; }

    /// <summary>
    /// Gets or sets the font's creation date, in seconds since 1904-01-01 00:00:00 UTC.
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the font's last modification date, in seconds since 1904-01-01 00:00:00 UTC.
    /// </summary>
    public long Modified { get; set; }

    /// <summary>
    /// Gets or sets the minimum x coordinate across all glyph bounding boxes.
    /// </summary>
    public short XMin { get; set; }

    /// <summary>
    /// Gets or sets the minimum y coordinate across all glyph bounding boxes.
    /// </summary>
    public short YMin { get; set; }

    /// <summary>
    /// Gets or sets the maximum x coordinate across all glyph bounding boxes.
    /// </summary>
    public short XMax { get; set; }

    /// <summary>
    /// Gets or sets the maximum y coordinate across all glyph bounding boxes.
    /// </summary>
    public short YMax { get; set; }

    /// <summary>
    /// Gets or sets the mac style bit field (bold, italic, ...).
    /// </summary>
    public ushort MacStyle { get; set; }

    /// <summary>
    /// Gets or sets the smallest readable size in pixels per em.
    /// </summary>
    public ushort LowestRecPpem { get; set; }

    /// <summary>
    /// Gets or sets the font direction hint. Deprecated by the spec; always 2 in practice.
    /// </summary>
    public short FontDirectionHint { get; set; }

    /// <summary>
    /// Gets or sets the format of the "loca" table: 0 for short (Offset16) entries, 1 for long
    /// (Offset32) entries.
    /// </summary>
    public short IndexToLocFormat { get; set; }

    /// <summary>
    /// Gets or sets the glyph data format. Always 0 for both TrueType and CFF outlines.
    /// </summary>
    public short GlyphDataFormat { get; set; }
}
