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
/// Draws a <see cref="PdfImageAlphaMode.ImageWithSoftAlphaMask"/> PDF image.
/// Composites image and soft mask via <see cref="ImageBlending.CreateSoftMaskBlendingShader"/>.
/// </summary>
internal sealed class DrawSoftMaskImageCommand : PdfCommand
{
    private readonly PdfImage _pdfImage;
    private readonly PdfImage _maskImage;
    private readonly ImageDecodingContext _context;
    private readonly PdfImageTileCacheEntry _imageCache;
    private readonly PdfImageTileCacheEntry _maskCache;
    private readonly SKColor? _matte;

    public DrawSoftMaskImageCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        _pdfImage = pdfImage;
        _maskImage = pdfImage.SoftMask;
        _context = context;

        var imageDecoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        var maskDecoder = PdfImageDecoder.GetDecoder(_maskImage, loggerFactory);

        var (imageTileInfo, maskTileInfo) = PdfImageCommandUtilities.ComputePairedTileSizes(pdfImage, _maskImage, context.DefaultTileSize);

        _imageCache = new PdfImageTileCacheEntry(imageDecoder, context, imageTileInfo);
        _maskCache = new PdfImageTileCacheEntry(maskDecoder, context, maskTileInfo);

        if (_maskImage.MatteArray != null)
        {
            _matte = _maskImage.ColorSpaceConverter.ToSrgb(
                _maskImage.MatteArray,
                _maskImage.RenderingIntent,
                default);
        }
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);

        var imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_pdfImage, ctm, executionContext);
        var maskRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_maskImage, ctm, executionContext);
        _imageCache.Initialize(ctm, imageRegion);
        _maskCache.Initialize(ctm, maskRegion);

        var sampling = PdfImageCommandUtilities.GetSamplingOptions(ctm, _context, _pdfImage);

        canvas.Save();
        canvas.Scale(1f / _pdfImage.Width, 1f / _pdfImage.Height);

        for (int i = 0; i < _imageCache.TileInfo.TotalTiles; i++)
        {
            PdfImageTile imageTile = _imageCache.GetNextTile(executionContext.CancellationToken);
            PdfImageTile maskTile = _maskCache.GetNextTile(executionContext.CancellationToken);

            if (imageTile.IsSkipped || maskTile.IsSkipped) continue;

            canvas.Save();
            canvas.ClipRect(imageTile.TilePosition);
            canvas.Translate(imageTile.TilePosition.Left, imageTile.TilePosition.Top);

            using SKShader imageShader = ImageBlending.BuildImageShader(imageTile.Image, new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height), sampling);
            using SKShader maskShader = ImageBlending.BuildImageShader(
                maskTile.Image,
                new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height),
                sampling);
            using SKShader blendingShader = ImageBlending.CreateSoftMaskBlendingShader(imageShader, maskShader, _matte);
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
        _maskCache.Dispose();
        base.Dispose(disposing);
    }
}
