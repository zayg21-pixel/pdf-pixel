using System;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Flags describing the characteristics of a PDF font, as defined in the PDF specification (Table 123).
/// Multiple flags may be combined.
/// </summary>
[Flags]
public enum PdfFontFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// All glyphs have the same width (monospaced font).
    /// </summary>
    FixedPitch = 1,

    /// <summary>
    /// Glyphs have serifs, which are short strokes drawn at an angle on the ends of the main strokes of the characters.
    /// </summary>
    Serif = 1 << 1,

    /// <summary>
    /// Font contains glyphs outside the Adobe standard Latin character set; it uses the Special encoding or no encoding.
    /// </summary>
    Symbolic = 1 << 2,

    /// <summary>
    /// Glyphs resemble cursive handwriting; the font may use a Script encoding.
    /// </summary>
    Script = 1 << 3,

    /// <summary>
    /// Font uses the Adobe standard Latin character set or a subset of it; the opposite of <see cref="Symbolic"/>.
    /// </summary>
    Nonsymbolic = 1 << 5,

    /// <summary>
    /// Glyphs are slanted to the right (italicised).
    /// </summary>
    Italic = 1 << 6,

    /// <summary>
    /// Font contains no lowercase letters; typically used for display or decorative fonts.
    /// </summary>
    AllCap = 1 << 16,

    /// <summary>
    /// Font contains both uppercase and lowercase letters, with lowercase letters displayed as smaller uppercase letters.
    /// </summary>
    SmallCap = 1 << 17,

    /// <summary>
    /// Bold glyphs should be painted with extra pixels even at small sizes, to make them appear bolder.
    /// </summary>
    ForceBold = 1 << 18
}
