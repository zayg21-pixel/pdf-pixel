namespace PdfPixel.Imaging.Model;

/// <summary>
/// Describes where a JPEG 2000 image's opacity comes from (/SMaskInData).
/// Applies only to images encoded with the JPXDecode filter.
/// </summary>
public enum PdfImageSoftMaskInData
{
    /// <summary>
    /// The codestream's opacity channel, if any, is ignored; opacity comes from
    /// <see cref="PdfImage.SoftMask"/> when present.
    /// </summary>
    None = 0,

    /// <summary>
    /// The codestream's opacity channel is the image's soft mask.
    /// </summary>
    Alpha = 1,

    /// <summary>
    /// The codestream's opacity channel is the image's soft mask, and the color channels
    /// are premultiplied with a backdrop color.
    /// </summary>
    PremultipliedAlpha = 2
}
