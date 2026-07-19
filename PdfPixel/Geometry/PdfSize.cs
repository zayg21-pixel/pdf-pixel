using SkiaSharp;

namespace PdfPixel.Geometry;

/// <summary>
/// A size defined by its width and height.
/// </summary>
public readonly struct PdfSize
{
    /// <summary>
    /// Initializes a new <see cref="PdfSize"/> from its dimensions.
    /// </summary>
    public PdfSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Height.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// The zero-sized instance.
    /// </summary>
    public static PdfSize Empty { get; } = new(0, 0);

    /// <summary>
    /// Converts this size to an <see cref="SKSize"/>.
    /// </summary>
    internal SKSize ToSkSize() => new(Width, Height);
}
