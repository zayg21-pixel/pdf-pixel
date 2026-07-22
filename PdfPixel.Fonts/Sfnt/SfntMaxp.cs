namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "maxp" table. Version 0.5 (CFF-flavored fonts) carries only
/// <see cref="NumGlyphs"/>; version 1.0 (TrueType-flavored fonts) additionally carries the outline
/// complexity limits below, which are null when absent.
/// </summary>
public class SfntMaxp
{
    /// <summary>
    /// Gets or sets the table's version number: 0.5 for CFF outlines, 1.0 for TrueType outlines.
    /// </summary>
    public float Version { get; set; }

    /// <summary>
    /// Gets or sets the number of glyphs in the font.
    /// </summary>
    public ushort NumGlyphs { get; set; }

    /// <summary>
    /// Gets or sets the maximum points in a non-composite glyph. TrueType only.
    /// </summary>
    public ushort? MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets the maximum contours in a non-composite glyph. TrueType only.
    /// </summary>
    public ushort? MaxContours { get; set; }

    /// <summary>
    /// Gets or sets the maximum points in a composite glyph. TrueType only.
    /// </summary>
    public ushort? MaxCompositePoints { get; set; }

    /// <summary>
    /// Gets or sets the maximum contours in a composite glyph. TrueType only.
    /// </summary>
    public ushort? MaxCompositeContours { get; set; }

    /// <summary>
    /// Gets or sets 1 if instructions do not use the twilight zone, or 2 if they do. TrueType only.
    /// </summary>
    public ushort? MaxZones { get; set; }

    /// <summary>
    /// Gets or sets the maximum points used in the twilight zone. TrueType only.
    /// </summary>
    public ushort? MaxTwilightPoints { get; set; }

    /// <summary>
    /// Gets or sets the number of storage area locations. TrueType only.
    /// </summary>
    public ushort? MaxStorage { get; set; }

    /// <summary>
    /// Gets or sets the number of function definitions. TrueType only.
    /// </summary>
    public ushort? MaxFunctionDefs { get; set; }

    /// <summary>
    /// Gets or sets the number of instruction definitions. TrueType only.
    /// </summary>
    public ushort? MaxInstructionDefs { get; set; }

    /// <summary>
    /// Gets or sets the maximum stack depth. TrueType only.
    /// </summary>
    public ushort? MaxStackElements { get; set; }

    /// <summary>
    /// Gets or sets the maximum byte count for glyph instructions. TrueType only.
    /// </summary>
    public ushort? MaxSizeOfInstructions { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of components referenced at the top level of a composite
    /// glyph. TrueType only.
    /// </summary>
    public ushort? MaxComponentElements { get; set; }

    /// <summary>
    /// Gets or sets the maximum levels of recursive component nesting. TrueType only.
    /// </summary>
    public ushort? MaxComponentDepth { get; set; }
}
