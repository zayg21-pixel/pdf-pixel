using PdfPixel.Commands.Image;
using SkiaSharp;
using System;
using System.Collections.Generic;

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

    private PdfCommandExecutionContext? _initializedContext;

    internal InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, SKSizeI imageSize)
    {
        _tileCache = tileCache;
        _imageSize = imageSize;
    }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        if (!ReferenceEquals(executionContext, _initializedContext))
        {
            throw new InvalidOperationException("Execution context changed between Initialize and Execute.");
        }

        _tileCache.ResetTileIndexes();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _tileCache.Dispose();
}
