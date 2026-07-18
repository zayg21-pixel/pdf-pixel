using PdfPixel.Commands.Image;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Initializes the tile cache for an image before its per-tile draw commands run, computing the
/// region of interest and marking the execution context as partial content when the region is
/// smaller than the full image.
/// </summary>
public sealed class InitializeTileCacheCommand : PdfCommand
{
    private readonly PdfImageTileCacheEntry _tileCache;
    private readonly SKSizeI _imageSize;

    internal InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, SKSizeI imageSize)
    {
        _tileCache = tileCache;
        _imageSize = imageSize;
    }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        SKMatrix ctm = PdfImageCommandUtilities.GetImageCtm(CommandHelpers.GetScaledMatrix(executionContext));
        SKRectI imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_imageSize, executionContext);

        if (imageRegion.Width != _imageSize.Width || imageRegion.Height != _imageSize.Height)
        {
            executionContext.SetPartialContent();
        }

        _tileCache.Initialize(ctm, imageRegion, executionContext.ContentLocker, executionContext.ExecutionObserver);
        _tileCache.ResetTileIndex();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _tileCache.Dispose();

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(InitializeTileCacheCommand)} {_tileCache.Decoder.Image.Type}, {_imageSize.Width}x{_imageSize.Height}, {_tileCache.Decoder.Image.BitsPerComponent}bpc, {_tileCache.TileInfo.TotalTiles} tiles";
}
