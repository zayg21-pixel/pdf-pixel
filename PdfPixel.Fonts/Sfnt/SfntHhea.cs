namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "hhea" table.
/// </summary>
public class SfntHhea
{
    /// <summary>
    /// Gets or sets the table's version number. 1.0 for every known "hhea" table.
    /// </summary>
    public float Version { get; set; }

    /// <summary>
    /// Gets or sets the typographic ascent.
    /// </summary>
    public short Ascender { get; set; }

    /// <summary>
    /// Gets or sets the typographic descent (typically negative).
    /// </summary>
    public short Descender { get; set; }

    /// <summary>
    /// Gets or sets the typographic line gap.
    /// </summary>
    public short LineGap { get; set; }

    /// <summary>
    /// Gets or sets the maximum advance width across all glyphs.
    /// </summary>
    public ushort AdvanceWidthMax { get; set; }

    /// <summary>
    /// Gets or sets the minimum left side bearing across all glyphs.
    /// </summary>
    public short MinLeftSideBearing { get; set; }

    /// <summary>
    /// Gets or sets the minimum right side bearing across all glyphs.
    /// </summary>
    public short MinRightSideBearing { get; set; }

    /// <summary>
    /// Gets or sets the maximum extent (left side bearing + glyph width) across all glyphs.
    /// </summary>
    public short XMaxExtent { get; set; }

    /// <summary>
    /// Gets or sets the caret slope rise, used for italic fonts.
    /// </summary>
    public short CaretSlopeRise { get; set; }

    /// <summary>
    /// Gets or sets the caret slope run, used for italic fonts.
    /// </summary>
    public short CaretSlopeRun { get; set; }

    /// <summary>
    /// Gets or sets the caret's horizontal offset for non-slanted fonts.
    /// </summary>
    public short CaretOffset { get; set; }

    /// <summary>
    /// Gets or sets the metric data format. Always 0.
    /// </summary>
    public short MetricDataFormat { get; set; }

    /// <summary>
    /// Gets or sets the number of leading entries in the "hmtx" table that carry their own advance
    /// width. Remaining glyphs reuse the last entry's advance width.
    /// </summary>
    public ushort NumberOfHMetrics { get; set; }
}
