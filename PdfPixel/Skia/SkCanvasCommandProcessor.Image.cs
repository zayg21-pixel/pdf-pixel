using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Commands.Processing;
using PdfPixel.Imaging.Processing;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawImageTile(DrawImageTileCommand command)
    {
        PdfImageTileCacheEntry tileCache = command.TileCache;
        PdfImageTile tile = tileCache.GetTile(_executionContext, command.TileIndex);

        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            _executionContext, tileCache.TileInfo.ImageSize, tile.TilePosition, tileCache.Decoder.Image.Interpolate);

        using SKImage skImage = tile.Image.ToSkImage();
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(tileCache.Decoder.Context);
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.DrawImage(skImage, placement.PlacementRectangle.ToSkRect(), SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate), paint);
        _canvas.Restore();
    }
}
