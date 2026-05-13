using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws an <see cref="PdfImageAlphaMode.StencilMask"/> image (HasImageMask — the image data IS the stencil)
/// using <see cref="ImageBlending.CreateImageMaskBlendingShader"/>.
/// Always uses Nearest sampling — stencil images are 1-bit and interpolation corrupts the alpha shape.
/// </summary>
internal sealed class DrawStencilMaskCommand : PdfCommand
{
    private readonly PdfImage _pdfImage;
    private readonly bool _inverse;
    private readonly ImageDecodingContext _context;
    private readonly PdfImageTileCacheEntry _imageCache;

    public DrawStencilMaskCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        _pdfImage = pdfImage;
        _context = context;
        _inverse = pdfImage.DecodeArray == null ||
                   (pdfImage.DecodeArray.Length == 2 && pdfImage.DecodeArray[0] < pdfImage.DecodeArray[1]);

        var decoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        var tileInfo = new PdfTileInfo(new SKSizeI(pdfImage.Width, pdfImage.Height), new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        _imageCache = new PdfImageTileCacheEntry(decoder, context, tileInfo);
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);

        var imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_pdfImage, ctm, executionContext);
        _imageCache.Initialize(ctm, imageRegion);

        canvas.Save();
        canvas.Scale(1f / _pdfImage.Width, 1f / _pdfImage.Height);

        for (int i = 0; i < _imageCache.TileInfo.TotalTiles; i++)
        {
            PdfImageTile tile = _imageCache.GetNextTile(executionContext.CancellationToken);
            if (tile.IsSkipped) continue;

            canvas.Save();
            canvas.ClipRect(tile.TilePosition);
            canvas.Translate(tile.TilePosition.Left, tile.TilePosition.Top);

            using SKShader stencilShader = ImageBlending.BuildImageShader(tile.Image, new SKSizeI(tile.TilePosition.Width, tile.TilePosition.Height), new SKSamplingOptions(SKFilterMode.Nearest));
            using SKShader blendingShader = ImageBlending.CreateImageMaskBlendingShader(stencilShader, _context.FillColor, _inverse);
            using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context);
            CommandHelpers.ApplyModifiers(paint, modifiers);

            canvas.DrawPaint(paint);
            canvas.Restore();
        }

        canvas.Restore();
    }

    protected override void Dispose(bool disposing)
    {
        _imageCache.Dispose();
        base.Dispose(disposing);
    }
}
