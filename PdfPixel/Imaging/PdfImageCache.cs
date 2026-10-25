using PdfPixel.Geometry;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging;

/// <summary>
/// Images a document keeps for reuse. An inline image is kept under its <see cref="InlineImageKey"/>. An inline image that
/// repeats, in the same content stream or in another one such as a form or the glyph procedure of a Type3 font, is read once
/// and shared.
/// </summary>
internal sealed class PdfImageCache
{
    private readonly Dictionary<InlineImageKey, PdfImage> _inlineImages = [];
    private readonly Dictionary<PdfImage, PdfPath> _stencilOutlines = [];

    /// <summary>
    /// Returns the inline image kept under <paramref name="key"/>, or null when none is.
    /// </summary>
    public PdfImage? GetInlineImage(in InlineImageKey key)
    {
        _inlineImages.TryGetValue(key, out PdfImage? image);

        return image;
    }

    /// <summary>
    /// Keeps <paramref name="image"/> as the inline image under <paramref name="key"/>.
    /// </summary>
    public void AddInlineImage(in InlineImageKey key, PdfImage image)
        => _inlineImages[key] = image ?? throw new ArgumentNullException(nameof(image));

    /// <summary>
    /// Returns the outline kept for the stencil mask <paramref name="image"/>, or null when none is.
    /// </summary>
    public PdfPath? GetStencilOutline(PdfImage image)
    {
        _stencilOutlines.TryGetValue(image, out PdfPath? outline);

        return outline;
    }

    /// <summary>
    /// Keeps <paramref name="outline"/> as the outline of the stencil mask <paramref name="image"/>.
    /// </summary>
    public void AddStencilOutline(PdfImage image, PdfPath outline)
        => _stencilOutlines[image] = outline ?? throw new ArgumentNullException(nameof(outline));

    /// <summary>
    /// Drops every kept image.
    /// </summary>
    public void Clear()
    {
        _inlineImages.Clear();
        _stencilOutlines.Clear();
    }
}
