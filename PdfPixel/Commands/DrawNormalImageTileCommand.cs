using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws one tile of an image with no mask, placing it directly onto the canvas.
/// </summary>
public sealed class DrawNormalImageTileCommand : PdfCommand
{
    private readonly NormalImageExecutionContext _context;

    internal DrawNormalImageTileCommand(NormalImageExecutionContext context) => _context = context;

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => _context.TileCache.InitializeNextTile(executionContext.ExecutionObserver);

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile tile = _context.TileCache.GetNextTile();
        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            executionContext, _context.ImageSize, tile.TilePosition, _context.Interpolate);

        using SKShader shader = ImageBlending.BuildImageShader(tile.Image, tile.SourceRegion, placement.DeviceSize, placement.Sampling);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(shader, _context.DecodingContext);
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
