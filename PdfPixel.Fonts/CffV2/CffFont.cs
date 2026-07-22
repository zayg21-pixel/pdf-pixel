using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a single font within a CFF typeface: its PostScript name and its own Top DICT.
/// </summary>
public class CffFont
{
    /// <summary>
    /// Gets or sets the PostScript name of this font (its Name INDEX entry).
    /// </summary>
    public PdfFontString Name { get; set; }

    /// <summary>
    /// Gets or sets this font's own Top DICT and (for a non-CID font) Private DICT, in the same shape
    /// as an FDArray entry. A CID-keyed font's per-glyph Private DICT instead comes from
    /// <see cref="FdArray"/>, by way of <see cref="FdSelect"/>.
    /// </summary>
    public CffFontDict Dict { get; set; } = new();

    /// <summary>
    /// Gets or sets the parsed FDArray (Font DICT INDEX) entries. Empty for non-CID-keyed fonts.
    /// </summary>
    public CffFontDict[] FdArray { get; set; } = Array.Empty<CffFontDict>();

    /// <summary>
    /// Gets or sets the FDSelect GID-to-Font-DICT-index mapping. Null for non-CID-keyed fonts.
    /// </summary>
    public CffFdSelect? FdSelect { get; set; }

    /// <summary>
    /// Gets or sets this font's parsed charset: the per-GID SID (name-keyed fonts) or CID (CID-keyed
    /// fonts) assignment. Null if the charset was structurally malformed.
    /// </summary>
    public CffCharset? Charset { get; set; }

    /// <summary>
    /// Gets or sets this font's parsed Encoding: the character-code-to-GID assignment. Null for
    /// CID-keyed fonts, which select glyphs by CID via <see cref="Charset"/> instead, or if the
    /// encoding was structurally malformed.
    /// </summary>
    public CffEncoding? Encoding { get; set; }

    /// <summary>
    /// Gets or sets this font's evaluated characters (one per glyph, indexed by GID): outline,
    /// repacked charstring, and width.
    /// </summary>
    public CffCharacter[] Characters { get; set; } = Array.Empty<CffCharacter>();
}
