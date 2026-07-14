namespace PdfPixel.Imaging.Model;

/// <summary>
/// Describes how alpha transparency is applied to a PDF image during rendering.
/// </summary>
public enum PdfImageAlphaMode
{
    /// <summary>
    /// No alpha.
    /// </summary>
    Normal,

    /// <summary>
    /// <see cref="PdfImage.HasImageMask"/> is true, background is defined by background color or pattern,
    /// image contents set alpha transparency. <see cref="PdfImage.Decode"/> controls mask inversion.
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
