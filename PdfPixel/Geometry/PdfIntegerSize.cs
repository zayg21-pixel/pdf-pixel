using System.Globalization;

namespace PdfPixel.Geometry;

/// <summary>
/// An integer-valued size defined by its width and height.
/// </summary>
public readonly struct PdfIntegerSize
{
    /// <summary>
    /// Initializes a new <see cref="PdfIntegerSize"/> from its dimensions.
    /// </summary>
    public PdfIntegerSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// The zero-sized instance.
    /// </summary>
    public static PdfIntegerSize Empty { get; } = new(0, 0);

    /// <summary>
    /// Whether this size equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => Equals(Empty);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Width.ToString(CultureInfo.InvariantCulture)} {Height.ToString(CultureInfo.InvariantCulture)}]";
}
