using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws one tile of an image combined with a separate stencil mask image (1-bit /Mask), blending the image through the mask onto the canvas.
/// </summary>
public sealed class DrawStencilMaskedImageTileCommand : PdfCommand
{
    private readonly StencilMaskedImageExecutionContext _context;

    internal DrawStencilMaskedImageTileCommand(StencilMaskedImageExecutionContext context) => _context = context;

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        _context.ImageCache.InitializeNextTile(executionContext.ExecutionObserver);
        _context.MaskCache.InitializeNextTile(executionContext.ExecutionObserver);
    }

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile imageTile = _context.ImageCache.GetNextTile();
        PdfImageTile maskTile = _context.MaskCache.GetNextTile();
        if (imageTile.IsSkipped || maskTile.IsSkipped || imageTile.Image == null || maskTile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            executionContext, _context.ImageSize, imageTile.TilePosition, _context.Interpolate);

        using SKShader imageShader = ImageBlending.BuildImageShader(imageTile.Image, imageTile.SourceRegion, placement.DeviceSize, placement.Sampling);
        using SKShader maskShader = ImageBlending.BuildImageShader(maskTile.Image, maskTile.SourceRegion, placement.DeviceSize, placement.Sampling);
        using SKShader blendingShader = ImageBlending.CreateStencilMaskShader(imageShader, maskShader, inverse: _context.InvertMask);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, modifiers);

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(placement.PlacementMatrix);
        executionContext.Canvas.ClipRect(placement.PlacementRectangle, antialias: placement.IsAntialiased);
        executionContext.Canvas.DrawPaint(paint);
        executionContext.Canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _context.Dispose();
}
