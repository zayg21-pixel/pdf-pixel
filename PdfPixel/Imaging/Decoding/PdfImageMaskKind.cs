namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// The mask an image is drawn with (ISO 32000-2 8.9.6, 11.6.5.2).
/// </summary>
public enum PdfImageMaskKind
{
    /// <summary>
    /// No mask.
    /// </summary>
    None,

    /// <summary>
    /// A soft-mask image, /SMask.
    /// </summary>
    SoftMask,

    /// <summary>
    /// The opacity channel of a JPEG 2000 codestream, /SMaskInData 1.
    /// </summary>
    SoftMaskInData,

    /// <summary>
    /// The opacity channel of a JPEG 2000 codestream the colors are premultiplied with, /SMaskInData 2.
    /// </summary>
    PremultipliedSoftMaskInData,

    /// <summary>
    /// A stencil mask image, /Mask.
    /// </summary>
    Mask,

    /// <summary>
    /// Color key masking, /Mask as an array of ranges.
    /// </summary>
    ColorKey
}
