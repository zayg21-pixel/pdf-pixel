using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Imaging;

namespace PdfPixel.Commands;

/// <summary>
/// Draws an image with the fill paint it was drawn with.
/// </summary>
public sealed class DrawImageCommand : PdfCommand, IPaintCommand
{
    private static readonly PdfRectangle UnitSquare = new(0, 0, 1, 1);

    internal DrawImageCommand(PdfImage image, PdfPaint paint, PdfGroupSource? source)
    {
        Image = image;
        Paint = paint;
        Source = source;
    }

    /// <summary>
    /// Gets the image, which holds its decoder and its masks.
    /// </summary>
    public PdfImage Image { get; }

    /// <summary>
    /// Gets the group a stencil mask is painted with, or <see langword="null"/> when it is painted in the fill paint's
    /// color.
    /// </summary>
    public PdfGroupSource? Source { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public PdfRectangle? Bounds => UnitSquare;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawImage;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawImageCommand)} {Image}";
}
