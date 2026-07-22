using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "name" table: every name record it contains.
/// </summary>
public class SfntName
{
    /// <summary>
    /// Gets or sets every name record found in the table.
    /// </summary>
    public IReadOnlyList<SfntNameRecord> Records { get; set; } = [];
}
