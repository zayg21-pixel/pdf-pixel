using PdfPixel.Color;
using PdfPixel.Geometry;

namespace PdfPixel.Imaging;

/// <summary>
/// Parameters controlling how <see cref="PdfImage.DecodePixels"/> decodes an image.
/// </summary>
public sealed class PdfImageDecodingParameters
{
    /// <summary>
    /// Gets a new instance holding the default values.
    /// </summary>
    public static PdfImageDecodingParameters Default => new();

    /// <summary>
    /// When true, the image's mask (/SMask, /SMaskInData or /Mask) is applied as alpha. Defaults to true.
    /// </summary>
    public bool ApplyMask { get; set; } = true;

    /// <summary>
    /// Rendering intent images without their own /Intent are converted with. Defaults to RelativeColorimetric.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; set; } = PdfRenderingIntent.RelativeColorimetric;

    /// <summary>
    /// Whether black point compensation is applied in color conversions. Defaults to true.
    /// </summary>
    public bool UseBlackPointCompensation { get; set; } = true;

    /// <summary>
    /// Size the decoded image is reduced to fit within, keeping its aspect ratio, or <see langword="null"/>
    /// to decode at full size.
    /// </summary>
    public PdfIntegerSize? MaximumSize { get; set; }
}
