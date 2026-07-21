namespace PdfPixel.Color.Paint;

/// <summary>
/// Drop-shadow parameters for a <see cref="PdfPaint"/>, matching the inputs of Skia's drop-shadow
/// image filter so any paint can carry an arbitrary shadow of any color.
/// </summary>
public sealed class PdfPaintShadowEffect
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPaintShadowEffect"/> class.
    /// </summary>
    public PdfPaintShadowEffect(float offsetX, float offsetY, float sigmaX, float sigmaY, in PdfColor color)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        SigmaX = sigmaX;
        SigmaY = sigmaY;
        Color = color;
    }

    /// <summary>
    /// Horizontal shadow offset.
    /// </summary>
    public float OffsetX { get; }

    /// <summary>
    /// Vertical shadow offset.
    /// </summary>
    public float OffsetY { get; }

    /// <summary>
    /// Horizontal blur sigma.
    /// </summary>
    public float SigmaX { get; }

    /// <summary>
    /// Vertical blur sigma.
    /// </summary>
    public float SigmaY { get; }

    /// <summary>
    /// Shadow color.
    /// </summary>
    public PdfColor Color { get; }
}
