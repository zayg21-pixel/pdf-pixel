using PdfPixel.Geometry;

namespace PdfPixel.TextExtraction;

/// <summary>
/// The characters of a <see cref="PdfWord"/> on one text line.
/// </summary>
public sealed class PdfWordPart
{
    /// <summary>
    /// Initializes a new <see cref="PdfWordPart"/> with the given geometry and character data.
    /// </summary>
    public PdfWordPart(in PdfRectangle boundingBox, int lineIndex, int startIndex, PdfCharacter[] characters)
    {
        BoundingBox = boundingBox;
        LineIndex = lineIndex;
        StartIndex = startIndex;
        Characters = characters;
    }

    /// <summary>
    /// Bounding box of the part in page coordinates.
    /// </summary>
    public PdfRectangle BoundingBox { get; }

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
    public PdfCharacter[] Characters { get; }
}
