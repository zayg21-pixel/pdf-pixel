using SkiaSharp;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Represents a word extracted from a PDF page, with its bounding box, type, line index, and characters.
/// </summary>
public class PdfWord
{
    /// <summary>
    /// Initializes a new <see cref="PdfWord"/> with the given geometry and character data.
    /// </summary>
    public PdfWord(SKRect boundingBox, PdfWordType type, int lineIndex, PdfCharacter[] characters)
    {
        BoundingBox = boundingBox;
        Type = type;
        LineIndex = lineIndex;
        Characters = characters;
    }

    /// <summary>
    /// Bounding box of the word in page coordinates.
    /// </summary>
    public SKRect BoundingBox { get; }

    /// <summary>
    /// Whether this token is a normal word or a punctuation mark.
    /// </summary>
    public PdfWordType Type { get; }

    /// <summary>
    /// Zero-based index of the text line this word belongs to.
    /// </summary>
    public int LineIndex { get; }

    /// <summary>
    /// The individual characters that make up this word.
    /// </summary>
    public PdfCharacter[] Characters { get; }
}
