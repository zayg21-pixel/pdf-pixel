using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using SkiaSharp;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a <see cref="PdfImageAlphaMode.ImageWithStencilMask"/> PDF image.
/// Composites image and its external stencil mask via <see cref="ImageBlending.CreateStencilMaskShader"/>.
/// </summary>
internal sealed class DrawStencilMaskedImageCommand : DrawImageCommandBase
{
    private readonly PdfImage _pdfImage;
    private readonly PdfImageCacheEntry _imageCache;
    private readonly PdfImageCacheEntry _maskCache;

    public DrawStencilMaskedImageCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(context)
    {
        _pdfImage = pdfImage;
        _imageCache = new PdfImageCacheEntry(PdfImageDecoder.GetDecoder(pdfImage, loggerFactory), context);
        _maskCache = new PdfImageCacheEntry(PdfImageDecoder.GetDecoder(pdfImage.StencilMask, loggerFactory), context);
    }

    protected override SKShader BuildShader(SKMatrix ctm, CancellationToken cancellationToken)
    {
        var image = _imageCache.Getmage(ctm, cancellationToken);
        if (image == null)
        {
            return null;
        }

        var mask = _maskCache.Getmage(ctm, cancellationToken);
        if (mask == null)
        {
            return null;
        }

        var sampling = PdfImageCommandUtilities.GetSamplingOptions(Context, _pdfImage);
        using var imageShader = ImageBlending.BuildImageShader(image, sampling);
        using var maskShader = ImageBlending.BuildImageShader(mask, sampling);
        return ImageBlending.CreateStencilMaskShader(imageShader, maskShader);
    }

    protected override void Dispose(bool disposing)
    {
        _imageCache.Dispose();
        _maskCache.Dispose();
        base.Dispose(disposing);
    }
}
