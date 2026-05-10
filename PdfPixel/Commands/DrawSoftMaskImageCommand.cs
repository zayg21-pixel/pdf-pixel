using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using SkiaSharp;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a <see cref="PdfImageAlphaMode.ImageWithSoftAlphaMask"/> PDF image.
/// Composites image and soft mask via <see cref="ImageBlending.CreateSoftMaskBlendingShader"/>.
/// </summary>
internal sealed class DrawSoftMaskImageCommand : DrawImageCommandBase
{
    private readonly PdfImage _pdfImage;
    private readonly PdfImageCacheEntry _imageCache;
    private readonly PdfImageCacheEntry _maskCache;
    private readonly SKColor? _matte;

    public DrawSoftMaskImageCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(context)
    {
        _pdfImage = pdfImage;
        _imageCache = new PdfImageCacheEntry(PdfImageDecoder.GetDecoder(pdfImage, loggerFactory), context);
        _maskCache = new PdfImageCacheEntry(PdfImageDecoder.GetDecoder(pdfImage.SoftMask, loggerFactory), context);

        if (pdfImage.SoftMask.MatteArray != null)
        {
            _matte = pdfImage.SoftMask.ColorSpaceConverter.ToSrgb(
                pdfImage.SoftMask.MatteArray,
                pdfImage.SoftMask.RenderingIntent,
                default);
        }
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
        return ImageBlending.CreateSoftMaskBlendingShader(imageShader, maskShader, _matte);
    }

    protected override void Dispose(bool disposing)
    {
        _imageCache.Dispose();
        _maskCache.Dispose();
        base.Dispose(disposing);
    }
}
