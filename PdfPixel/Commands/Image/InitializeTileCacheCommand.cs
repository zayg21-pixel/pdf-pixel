using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

internal sealed class InitializeTileCacheCommand : PdfCommand
{
    private readonly PdfImageTileCacheEntry _tileCache;
    private readonly SKSizeI _imageSize;
    private readonly IDisposable _ownedContext;

    public InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, SKSizeI imageSize, IDisposable ownedContext = null)
    {
        _tileCache = tileCache;
        _imageSize = imageSize;
        _ownedContext = ownedContext;
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);
        var imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_imageSize, ctm, executionContext);
        _tileCache.Initialize(ctm, imageRegion, executionContext.ContentLocker, executionContext.ExecutionObserver);
    }

    protected override void Dispose(bool disposing)
    {
        _ownedContext?.Dispose();
        base.Dispose(disposing);
    }
}
