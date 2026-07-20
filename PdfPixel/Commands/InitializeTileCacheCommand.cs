using PdfPixel.Commands.Image;
using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Initializes the tile cache for an image before its per-tile draw commands run, computing the
/// region of interest and marking the execution context as partial content when the region is
/// smaller than the full image.
/// </summary>
public sealed class InitializeTileCacheCommand : PdfCommand
{
    private readonly PdfImageTileCacheEntry _tileCache;
    private readonly PdfIntegerSize _imageSize;

    internal InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, in PdfIntegerSize imageSize)
    {
        _tileCache = tileCache;
        _imageSize = imageSize;
    }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        PdfMatrix ctm = PdfImageCommandUtilities.GetImageCtm(CommandHelpers.GetScaledMatrix(executionContext));
        PdfIntegerRectangle imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_imageSize, executionContext);

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
