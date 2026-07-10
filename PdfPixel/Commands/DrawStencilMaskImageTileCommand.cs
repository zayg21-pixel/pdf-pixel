using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Draws one tile of an image mask (1-bit stencil), blending the fill color through the stencil onto the canvas.
/// </summary>
public sealed class DrawStencilMaskImageTileCommand : PdfCommand
{
    private readonly StencilMaskImageExecutionContext _context;
    private readonly SKColorFilter _colorFilter;

    internal DrawStencilMaskImageTileCommand(StencilMaskImageExecutionContext context)
    {
        _context = context;
        SKColor fillColor = _context.DecodingContext.FillColor;
        _colorFilter = ImageBlending.CreateImageMaskColorFilter(in fillColor, _context.InvertMask);
    }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        PdfImageTile tile = _context.TileCache.GetNextTile(executionContext.ExecutionObserver);
        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(executionContext, _context.ImageSize, tile.TilePosition, _context.Interpolate);

        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(_context.DecodingContext);
        paint.ColorFilter = _colorFilter;
        CommandHelpers.ApplyModifiers(paint, executionContext);

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(placement.PlacementMatrix);
        executionContext.Canvas.DrawImage(tile.Image, placement.PlacementRectangle, placement.Sampling, paint);
        executionContext.Canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _context.Dispose();
        _colorFilter.Dispose();
    }
}
