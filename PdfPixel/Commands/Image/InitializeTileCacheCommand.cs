using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

internal sealed class InitializeTileCacheCommand : PdfCommand
{
    private readonly PdfImageTileCacheEntry _tileCache;
    private readonly SKSizeI _imageSize;

    private PdfCommandExecutionContext? _initializedContext;

    public InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, SKSizeI imageSize)
    {
        _tileCache = tileCache;
        _imageSize = imageSize;
    }

    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        _initializedContext = executionContext;

        SKMatrix ctm = CommandHelpers.GetScaledMatrix(executionContext);
        SKRectI imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(_imageSize, executionContext.Frames.TotalMatrix, executionContext);

        if (imageRegion.Width != _imageSize.Width || imageRegion.Height != _imageSize.Height)
        {
            executionContext.SetPartialContent();
        }

        _tileCache.Initialize(ctm, imageRegion, executionContext.ContentLocker, executionContext.ExecutionObserver, executionContext.ImageCache);
    }

    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        if (!ReferenceEquals(executionContext, _initializedContext))
        {
            throw new InvalidOperationException("Execution context changed between Initialize and Execute.");
        }

        _tileCache.ResetTileIndexes();
    }

    protected override void Dispose(bool disposing) => _tileCache.Dispose();
}
