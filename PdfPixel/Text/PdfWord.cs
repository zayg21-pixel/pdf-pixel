using PdfPixel.Geometry;
using System;

namespace PdfPixel.Text;

/// <summary>
/// Represents a word extracted from a PDF page, with its bounding box, type, and parts.
/// </summary>
public class PdfWord
{
    private readonly ReadOnlyMemory<PdfWordPart> _parts;

    /// <summary>
    /// Initializes a new <see cref="PdfWord"/> of the given type from its parts in reading order.
    /// </summary>
    public PdfWord(PdfWordType type, in ReadOnlyMemory<PdfWordPart> parts)
    {
        if (parts.IsEmpty)
        {
            throw new ArgumentException("A word has at least one part.", nameof(parts));
        }

        Type = type;
        _parts = parts;
    }

    /// <summary>
    /// Bounding box of the word in page coordinates: the union of its parts' bounding boxes.
    /// </summary>
    public PdfRectangle BoundingBox
    {
        get
        {
            ReadOnlySpan<PdfWordPart> parts = _parts.Span;
            PdfRectangle boundingBox = parts[0].BoundingBox;

            for (int index = 1; index < parts.Length; index++)
            {
                boundingBox = PdfRectangle.Union(boundingBox, parts[index].BoundingBox);
            }

            return boundingBox;
        }
    }

    /// <summary>
    /// Whether this token is a normal word, a punctuation mark or whitespace.
    /// </summary>
    public PdfWordType Type { get; }

    /// <summary>
    /// Parts of the word in reading order, one per text line. A word hyphenated across lines has more than one part;
    /// every part but the last ends with the hyphen and a generated line break.
    /// </summary>
    public ReadOnlySpan<PdfWordPart> Parts => _parts.Span;
}
