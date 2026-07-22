namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single glyph's entry in the SFNT "hmtx" table.
/// </summary>
public readonly struct SfntHorizontalMetric
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntHorizontalMetric"/> struct.
    /// </summary>
    /// <param name="advanceWidth">The glyph's advance width, in font design units.</param>
    /// <param name="leftSideBearing">The glyph's left side bearing, in font design units.</param>
    public SfntHorizontalMetric(ushort advanceWidth, short leftSideBearing)
    {
        AdvanceWidth = advanceWidth;
        LeftSideBearing = leftSideBearing;
    }

    /// <summary>
    /// Gets the glyph's advance width, in font design units.
    /// </summary>
    public ushort AdvanceWidth { get; }

    /// <summary>
    /// Gets the glyph's left side bearing, in font design units.
    /// </summary>
    public short LeftSideBearing { get; }
}
