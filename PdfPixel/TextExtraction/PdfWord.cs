using PdfPixel.Geometry;
using System;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Represents a word extracted from a PDF page, with its bounding box, type, and parts.
/// </summary>
public class PdfWord
{
    /// <summary>
    /// Initializes a new <see cref="PdfWord"/> of the given type from its parts in reading order.
    /// </summary>
    public PdfWord(PdfWordType type, PdfWordPart[] parts)
    {
        Type = type;
        Parts = parts ?? throw new ArgumentNullException(nameof(parts));

        PdfRectangle boundingBox = parts[0].BoundingBox;

        for (int index = 1; index < parts.Length; index++)
        {
            boundingBox = PdfRectangle.Union(boundingBox, parts[index].BoundingBox);
        }

        BoundingBox = boundingBox;
    }

    /// <summary>
    /// Bounding box of the word in page coordinates.
    /// </summary>
    public PdfRectangle BoundingBox { get; }

    /// <summary>
    /// Whether this token is a normal word, a punctuation mark or whitespace.
    /// </summary>
    public PdfWordType Type { get; }

    /// <summary>
    /// Parts of the word in reading order, one per text line. A word hyphenated across lines has more than one part;
    /// every part but the last ends with the hyphen and a generated line break.
    /// </summary>
    public PdfWordPart[] Parts { get; }

    /// <summary>
    /// Index of the first character of this word among the characters of every word of the page.
    /// </summary>
    public int StartIndex => Parts[0].StartIndex;
}
