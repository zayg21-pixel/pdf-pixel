namespace PdfPixel.Imaging.Model;

public enum PdfImageAlphaMode
{
    /// <summary>
    /// No alpha.
    /// </summary>
    Normal,

    /// <summary>
    /// <see cref="PdfImage.HasImageMask"/> is true, background is defined by background color or pattern,
    /// Image contents sets alpha transparency. <see cref="MaskArray"/> can contain properties to invert mask.
    /// </summary>
    StencilMask,

    /// <summary>
    /// Image transparency is defined by <see cref="PdfImage.SoftMask"/>.
    /// </summary>
    ImageWithSoftAlphaMask,

    /// <summary>
    /// Image transparency is defined by <see cref="PdfImage.StencilMask"/>.
    /// </summary>
    ImageWithStencilMask
}
