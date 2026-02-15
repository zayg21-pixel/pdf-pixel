using SkiaSharp;

namespace PdfPixel.TextExtraction;

public readonly struct PdfCharacter
{
    public PdfCharacter(string text, SKRect boundingBox)
    {
        Text = text;
        BoundingBox = boundingBox;
    }

    public string Text { get; }

    public SKRect BoundingBox { get; }
}
