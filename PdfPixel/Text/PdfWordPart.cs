using PdfPixel.Geometry;
using System;

namespace PdfPixel.Text;

/// <summary>
/// The characters of a <see cref="PdfWord"/> on one text line.
/// </summary>
public sealed class PdfWordPart
{
    private readonly ReadOnlyMemory<PdfCharacter> _characters;

    /// <summary>
    /// Initializes a new <see cref="PdfWordPart"/> with the given geometry and character data.
    /// </summary>
    public PdfWordPart(int lineIndex, int startIndex, in ReadOnlyMemory<PdfCharacter> characters)
    {
        if (characters.IsEmpty)
        {
            throw new ArgumentException("A word part has at least one character.", nameof(characters));
        }

        LineIndex = lineIndex;
        StartIndex = startIndex;
        _characters = characters;
    }

    /// <summary>
    /// Bounding box of the part in page coordinates: the union of its characters' bounding boxes.
    /// </summary>
    public PdfRectangle BoundingBox
    {
        get
        {
            ReadOnlySpan<PdfCharacter> characters = _characters.Span;
            PdfRectangle boundingBox = characters[0].BoundingBox;

            for (int index = 1; index < characters.Length; index++)
            {
                boundingBox = PdfRectangle.Union(boundingBox, characters[index].BoundingBox);
            }

            return boundingBox;
        }
    }

    /// <summary>
    /// Zero-based index of the text line this part belongs to.
    /// </summary>
    public int LineIndex { get; }

    /// <summary>
    /// Index of the first character of this part among the characters of every word of the page.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// The individual characters that make up this part.
    /// </summary>
    public ReadOnlySpan<PdfCharacter> Characters => _characters.Span;
}
