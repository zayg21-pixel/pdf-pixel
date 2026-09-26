namespace PdfPixel.TextExtraction;

/// <summary>
/// Classifies a <see cref="PdfWord"/> as a normal word or a punctuation token.
/// </summary>
public enum PdfWordType
{
    /// <summary>
    /// A regular word composed of alphanumeric characters.
    /// </summary>
    Normal,

    /// <summary>
    /// A punctuation mark treated as a separate word token.
    /// </summary>
    Punctuation,

    /// <summary>
    /// Whitespace between words, or a line break at the end of a line.
    /// </summary>
    Space
}
