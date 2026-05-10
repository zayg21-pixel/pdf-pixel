using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using SkiaSharp;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Draws an <see cref="PdfImageAlphaMode.StencilMask"/> image (HasImageMask — the image data IS the stencil)
/// using <see cref="ImageBlending.CreateImageMaskBlendingShader"/>.
/// Always uses Nearest sampling — stencil images are 1-bit and interpolation corrupts the alpha shape.
///
/// Compositing is fully determined by <see cref="DrawImageCommandBase.Context"/>:
/// standalone rendering carries the real fill colour and blend mode;
/// pattern-layer masking uses a context with FillColor=White, BlendMode=DstIn, FillAlpha=1
/// so the stencil alpha drives DstIn on the already-rendered pattern.
/// </summary>
internal sealed class DrawStencilMaskCommand : DrawImageCommandBase
{
    private readonly bool _inverse;
    private readonly PdfImageCacheEntry _imageCache;

    public DrawStencilMaskCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(context)
    {
        _inverse = pdfImage.DecodeArray == null ||
                   (pdfImage.DecodeArray.Length == 2 && pdfImage.DecodeArray[0] < pdfImage.DecodeArray[1]);

        _imageCache = new PdfImageCacheEntry(PdfImageDecoder.GetDecoder(pdfImage, loggerFactory), context);
    }

    protected override SKShader BuildShader(SKMatrix ctm, CancellationToken cancellationToken)
    {
        var stencil = _imageCache.Getmage(ctm, cancellationToken);
        if (stencil == null)
        {
            return null;
        }

        using var stencilShader = ImageBlending.BuildImageShader(stencil, new SKSamplingOptions(SKFilterMode.Nearest));
        return ImageBlending.CreateImageMaskBlendingShader(stencilShader, Context.FillColor, _inverse);
    }

    protected override void Dispose(bool disposing)
    {
        _imageCache.Dispose();
        base.Dispose(disposing);
    }
}
