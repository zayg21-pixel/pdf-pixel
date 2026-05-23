using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a <see cref="PdfImageAlphaMode.Normal"/> PDF image.
/// </summary>
internal sealed class DrawNormalImageCommand : PdfCommand
{
    private readonly PdfImage _pdfImage;
    private readonly ImageDecodingContext _context;
    private readonly PdfImageTileCacheEntry _tileCache;

    public DrawNormalImageCommand(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        _pdfImage = pdfImage;
        _context = context;
        var decoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        var tileInfo = new PdfTileInfo(new SKSizeI(pdfImage.Width, pdfImage.Height), new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        _tileCache = new PdfImageTileCacheEntry(decoder, context, tileInfo);
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);
        var imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_pdfImage, ctm, executionContext);
        _tileCache.Initialize(ctm, imageRegion, executionContext.ContentLocker, executionContext.ExecutionObserver);

        var samplingOptions = PdfImageCommandUtilities.GetSamplingOptions(ctm, _context, _pdfImage);

        canvas.Save();
        canvas.Scale(1f / _pdfImage.Width, 1f / _pdfImage.Height);

        for (int i = 0; i < _tileCache.TileInfo.TotalTiles; i++)
        {
            PdfImageTile tile = _tileCache.GetNextTile(executionContext.ExecutionObserver);
            if (tile.IsSkipped) continue;

            canvas.Save();
            canvas.ClipRect(tile.TilePosition);
            canvas.Translate(tile.TilePosition.Left, tile.TilePosition.Top);

            using SKShader shader = ImageBlending.BuildImageShader(tile.Image, new SKSizeI(tile.TilePosition.Width, tile.TilePosition.Height), samplingOptions);
            using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(shader, _context);
            CommandHelpers.ApplyModifiers(paint, modifiers);
            canvas.DrawPaint(paint);

            canvas.Restore();
        }

        canvas.Restore();
    }

    protected override void Dispose(bool disposing)
    {
        _tileCache.Dispose();
        base.Dispose(disposing);
    }
}
