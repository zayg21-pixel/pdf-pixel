using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "gasp" table: the ppem ranges over which the font asks for grid-fitting
/// and smoothing, in ascending order of their ppem limits.
/// </summary>
public class SfntGasp
{
    /// <summary>
    /// Gets or sets the table's version number: 0 for the original two behavior flags, 1 for the two
    /// additional symmetric ones.
    /// </summary>
    public ushort Version { get; set; }

    /// <summary>
    /// Gets or sets every ppem range found in the table, in ascending order of
    /// <see cref="SfntGaspRange.MaxPpem"/>, the last of which covers every remaining size.
    /// </summary>
    public IReadOnlyList<SfntGaspRange> Ranges { get; set; } = [];
}
