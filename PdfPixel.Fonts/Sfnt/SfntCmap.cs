using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "cmap" table: every subtable it contains. Read-only - "cmap" is never
/// written back from this model.
/// </summary>
public class SfntCmap
{
    /// <summary>
    /// Gets or sets every subtable found in the "cmap" table, in directory order.
    /// </summary>
    public IReadOnlyList<SfntCmapSubtable> Subtables { get; set; } = [];
}
