using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using SkiaSharp;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a <see cref="PdfImageAlphaMode.Normal"/> PDF image.
/// </summary>
internal sealed class DrawNormalImageCommand : DrawImageCommandBase
{
    private readonly PdfImage _pdfImage;
    private readonly PdfImageCacheEntry _imageCache;

    public DrawNormalImageCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(context)
    {
        _pdfImage = pdfImage;
        _imageCache = new PdfImageCacheEntry(PdfImageDecoder.GetDecoder(pdfImage, loggerFactory), context);
    }

    protected override SKShader BuildShader(SKMatrix ctm, CancellationToken cancellationToken)
    {
        var image = _imageCache.Getmage(ctm, cancellationToken);
        if (image == null)
        {
            return null;
        }

        return ImageBlending.BuildImageShader(image, PdfImageCommandUtilities.GetSamplingOptions(Context, _pdfImage));
    }

    protected override void Dispose(bool disposing)
    {
        _imageCache.Dispose();
        base.Dispose(disposing);
    }
}
