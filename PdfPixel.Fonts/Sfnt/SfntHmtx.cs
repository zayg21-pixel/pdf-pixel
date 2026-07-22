using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "hmtx" table: one entry per glyph, fully expanded. The "hmtx" table's own
/// trailing-repeat compression (per <c>hhea.numberOfHMetrics</c>) is an encoding detail resolved by
/// <see cref="SfntHmtxProcessor"/>, not represented here.
/// </summary>
public class SfntHmtx
{
    /// <summary>
    /// Gets or sets the per-glyph metrics, indexed by glyph ID.
    /// </summary>
    public IReadOnlyList<SfntHorizontalMetric> Metrics { get; set; } = [];
}
