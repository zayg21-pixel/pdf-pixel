using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

internal sealed class InitializeTileCacheCommand : PdfCommand
{
    private readonly PdfImageTileCacheEntry _tileCache;
    private readonly SKSizeI _imageSize;

    public InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, SKSizeI imageSize)
    {
        _tileCache = tileCache;
        _imageSize = imageSize;
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        SKMatrix ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);
        SKRectI imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_imageSize, ctm, executionContext);
        _tileCache.Initialize(ctm, imageRegion, executionContext.ContentLocker, executionContext.ExecutionObserver);
    }

    protected override void Dispose(bool disposing) => _tileCache.Dispose();
}
