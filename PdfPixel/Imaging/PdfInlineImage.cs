using PdfPixel.Geometry;
using System;

namespace PdfPixel.Imaging;

/// <summary>
/// An inline image read from a content stream, as the document keeps it: the image, with the outline it is filled as
/// when it is a small stencil mask.
/// </summary>
internal sealed class PdfInlineImage
{
    public PdfInlineImage(PdfImage image, PdfPath? stencilOutline)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        StencilOutline = stencilOutline;
    }

    /// <summary>
    /// The image.
    /// </summary>
    public PdfImage Image { get; }

    /// <summary>
    /// The outline of the painted samples of the image as a stencil mask, traced by <see cref="PdfStencilOutline"/>, or
    /// null when the image is drawn as an image.
    /// </summary>
    public PdfPath? StencilOutline { get; }
}
