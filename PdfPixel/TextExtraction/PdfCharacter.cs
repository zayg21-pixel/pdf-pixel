using SkiaSharp;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Represents a single extracted character with its Unicode text and bounding box.
/// </summary>
public readonly struct PdfCharacter
{
    /// <summary>
    /// Initializes a <see cref="PdfCharacter"/> with the given Unicode text and bounding box.
    /// </summary>
    public PdfCharacter(string? text, SKRect boundingBox)
    {
        Text = text;
        BoundingBox = boundingBox;
    }

    /// <summary>
    /// The Unicode text of this character, or <see langword="null"/> if it could not be mapped.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Bounding box of the character in page coordinates.
    /// </summary>
    public SKRect BoundingBox { get; }
}
