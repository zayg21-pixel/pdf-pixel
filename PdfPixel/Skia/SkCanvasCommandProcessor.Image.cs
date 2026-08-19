using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteInitializeTileCache(InitializeTileCacheCommand command)
    {
        PdfMatrix ctm = PdfImageCommandUtilities.GetImageCtm(CommandHelpers.GetScaledMatrix(_executionContext));
        PdfIntegerRectangle imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(command.ImageSize, _executionContext);

        command.TileCache.Initialize(ctm, imageRegion, _executionContext.ContentLocker, _executionContext.ExecutionObserver);
    }

    private void ExecuteDrawNormalImageTile(DrawNormalImageTileCommand command)
    {
        NormalImageExecutionContext context = command.Context;
        PdfImageTile tile = context.TileCache.GetNextTile(_executionContext.ExecutionObserver);

        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            _executionContext, context.ImageSize, tile.TilePosition, context.Interpolate);

        using SKImage skImage = tile.Image.ToSkImage();
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(context.DecodingContext);
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.DrawImage(skImage, placement.PlacementRectangle.ToSkRect(), SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate), paint);
        _canvas.Restore();
    }

    private void ExecuteDrawStencilMaskImageTile(DrawStencilMaskImageTileCommand command)
    {
        StencilMaskImageExecutionContext context = command.Context;
        PdfImageTile tile = context.TileCache.GetNextTile(_executionContext.ExecutionObserver);

        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(_executionContext, context.ImageSize, tile.TilePosition, context.Interpolate);

        SKColor fillColor = context.DecodingContext.FillPaint.Color.ToSkiaColor();
        using SKColorFilter colorFilter = SkiaImageBlending.CreateImageMaskColorFilter(in fillColor, context.InvertMask);
        using SKImage skImage = tile.Image.ToSkImage();
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(context.DecodingContext);
        paint.ColorFilter = colorFilter;
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.DrawImage(skImage, placement.PlacementRectangle.ToSkRect(), SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate), paint);
        _canvas.Restore();
    }
}
