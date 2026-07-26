using PdfPixel.Fonts.CffV2;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A decoded SFNT font: every table's raw bytes, plus the parsed model for each table that has a
/// dedicated processor. Tables without one remain accessible only through <see cref="Tables"/>, which
/// is mutable so a table can be added (e.g. a repacked "CFF " table when assembling a brand new font)
/// or removed before writing.
/// </summary>
public class SfntFont
{
    /// <summary>
    /// Gets or sets the sfnt version tag: 0x00010000 (or 'true') for TrueType outlines, 'OTTO' for
    /// CFF outlines.
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Gets or sets every table found in the font's table directory, in directory order.
    /// </summary>
    public List<SfntTableRecord> Tables { get; set; } = [];

    /// <summary>
    /// Gets or sets the parsed "head" table, or null if the font has none or it failed to parse.
    /// </summary>
    public SfntHead? Head { get; set; }

    /// <summary>
    /// Gets or sets the parsed "hhea" table, or null if the font has none or it failed to parse.
    /// </summary>
    public SfntHhea? Hhea { get; set; }

    /// <summary>
    /// Gets or sets the parsed "maxp" table, or null if the font has none or it failed to parse.
    /// </summary>
    public SfntMaxp? Maxp { get; set; }

    /// <summary>
    /// Gets or sets the parsed "hmtx" table, or null if the font has none, or "hhea"/"maxp" (which
    /// "hmtx" depends on to parse) are themselves missing or unparsable.
    /// </summary>
    public SfntHmtx? Hmtx { get; set; }

    /// <summary>
    /// Gets or sets the parsed "OS/2" table, or null if the font has none or it failed to parse.
    /// </summary>
    public SfntOs2? Os2 { get; set; }

    /// <summary>
    /// Gets or sets the parsed "name" table, or null if the font has none or it failed to parse.
    /// </summary>
    public SfntName? Name { get; set; }

    /// <summary>
    /// Gets or sets the parsed "cmap" table, or null if the font has none or it failed to parse.
    /// Read-only: edits here are never written back, "cmap" is always passed through as raw bytes.
    /// </summary>
    public SfntCmap? Cmap { get; set; }

    /// <summary>
    /// Gets or sets the parsed "post" table, or null if the font has none or it failed to parse.
    /// Read-only: edits here are never written back, "post" is always passed through as raw bytes.
    /// </summary>
    public SfntPost? Post { get; set; }

    /// <summary>
    /// Gets or sets the parsed "glyf" table, or null if the font has none (e.g. a CFF-flavored font),
    /// or "loca"/"maxp" (which "glyf" depends on to parse) are themselves missing or unparsable.
    /// </summary>
    public SfntGlyf? Glyf { get; set; }

    /// <summary>
    /// Gets or sets the parsed "CFF " table, or null if the font has none (e.g. a TrueType-flavored
    /// font) or it failed to parse. A non-null value identifies this font as CFF-flavored OpenType.
    /// </summary>
    public CffTypeface? CffTypeface { get; set; }
}
