using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using SkiaSharp;

namespace PdfPixel.Commands.Skia;

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

    private void ExecuteDrawSoftMaskImageTile(DrawSoftMaskImageTileCommand command)
    {
        SoftMaskImageExecutionContext context = command.Context;
        PdfImageTile imageTile = context.ImageCache.GetNextTile(_executionContext.ExecutionObserver);
        PdfImageTile maskTile = context.MaskCache.GetNextTile(_executionContext.ExecutionObserver);

        if (imageTile.Image == null || maskTile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            _executionContext, context.ImageSize, imageTile.TilePosition, context.Interpolate);

        SKColor? matte = null;
        if (context.MatteArray != null && maskTile.Parameters != null)
        {
            matte = maskTile.Parameters.ColorSpaceConverter.ToSrgb(context.MatteArray, maskTile.Parameters.RenderingIntent, default).ToSkiaColor();
        }

        SKSamplingOptions sampling = SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate);

        using SKImage skImage = imageTile.Image.ToSkImage();
        using SKImage skMaskImage = maskTile.Image.ToSkImage();
        using SKShader imageShader = SkiaImageBlending.BuildImageShader(skImage, placement.DeviceSize.ToSkSize(), sampling);
        using SKShader maskShader = SkiaImageBlending.BuildImageShader(skMaskImage, placement.DeviceSize.ToSkSize(), sampling);
        using SKShader blendingShader = SkiaImageBlending.CreateSoftMaskBlendingShader(imageShader, maskShader, matte);
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(blendingShader, context.DecodingContext);
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.ClipRect(placement.PlacementRectangle.ToSkRect());
        _canvas.DrawPaint(paint);
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

        SKColor fillColor = context.DecodingContext.FillColor.ToSkiaColor();
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
