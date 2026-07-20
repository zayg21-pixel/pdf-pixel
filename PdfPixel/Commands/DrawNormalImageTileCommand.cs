using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Globalization;

namespace PdfPixel.Commands;

/// <summary>
/// Draws one tile of an image with no mask, placing it directly onto the canvas.
/// </summary>
public sealed class DrawNormalImageTileCommand : PdfCommand
{
    private readonly NormalImageExecutionContext _context;
    private int? _lastTileIndex;

    internal DrawNormalImageTileCommand(NormalImageExecutionContext context) => _context = context;

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        PdfImageTile tile = _context.TileCache.GetNextTile(executionContext.ExecutionObserver);
        _lastTileIndex = tile.TileIndex;

        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            executionContext, _context.ImageSize, tile.TilePosition, _context.Interpolate);

        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(_context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, executionContext);

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(placement.PlacementMatrix);
        executionContext.Canvas.DrawImage(tile.Image, placement.PlacementRectangle, placement.Sampling, paint);
        executionContext.Canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _context.Dispose();

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(DrawNormalImageTileCommand)} tile={_lastTileIndex?.ToString(CultureInfo.InvariantCulture) ?? "not executed"}";
}
