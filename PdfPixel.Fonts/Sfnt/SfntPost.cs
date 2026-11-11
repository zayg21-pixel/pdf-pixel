using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "post" table. Read-only - "post" is never written back, only ever passed
/// through unchanged as raw bytes.
/// </summary>
public class SfntPost
{
    /// <summary>
    /// Gets or sets the italic angle in degrees counter-clockwise from vertical (0 for upright fonts).
    /// </summary>
    public float ItalicAngle { get; set; }

    /// <summary>
    /// Gets or sets the suggested underline position (negative values are below the baseline).
    /// </summary>
    public short UnderlinePosition { get; set; }

    /// <summary>
    /// Gets or sets the suggested underline thickness.
    /// </summary>
    public short UnderlineThickness { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the font is monospaced.
    /// </summary>
    public bool IsFixedPitch { get; set; }

    /// <summary>
    /// Gets the mapping from glyph name to glyph ID once <see cref="SfntFontProcessor.GetNameToGid"/> has read it,
    /// through which it is reached. Null for formats 2.5/3.0/4.0, which by spec carry no glyph names (format 3.0 - no
    /// names to save space - is the common case in practice).
    /// </summary>
    internal IReadOnlyDictionary<PdfFontString, ushort>? NameToGid { get; private set; }

    /// <summary>
    /// Gets whether <see cref="NameToGid"/> has been read.
    /// </summary>
    internal bool IsNameToGidResolved { get; private set; }

    /// <summary>
    /// Sets the mapping from glyph name to glyph ID read from the table.
    /// </summary>
    internal void SetNameToGid(IReadOnlyDictionary<PdfFontString, ushort>? nameToGid)
    {
        NameToGid = nameToGid;
        IsNameToGidResolved = true;
    }
}
