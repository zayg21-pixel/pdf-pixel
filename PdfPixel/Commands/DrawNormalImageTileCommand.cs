using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

internal sealed class DrawNormalImageTileCommand : PdfCommand
{
    private readonly NormalImageExecutionContext _context;

    public DrawNormalImageTileCommand(NormalImageExecutionContext context) => _context = context;

    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => _context.TileCache.InitializeNextTile(executionContext.ExecutionObserver);

    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile tile = _context.TileCache.GetNextTile();
        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SKMatrix ctm = CommandHelpers.GetScaledMatrix(executionContext);
        SKSamplingOptions sampling = PdfImageCommandUtilities.GetSamplingOptions(ctm, _context.ImageSize, _context.Interpolate);

        using SKShader shader = ImageBlending.BuildImageShader(tile.Image, tile.SourceRegion, new SKSizeI(tile.TilePosition.Width, tile.TilePosition.Height), sampling);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(shader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, modifiers);

        bool antialias = PdfImageCommandUtilities.GetImageTileIsAntialias(ctm, _context.ImageSize, executionContext);

        executionContext.Canvas.Save();
        executionContext.Canvas.Scale(1f / _context.ImageSize.Width, 1f / _context.ImageSize.Height);
        executionContext.Canvas.ClipRect(tile.TilePosition, antialias: antialias);
        executionContext.Canvas.Translate(tile.TilePosition.Left, tile.TilePosition.Top);
        executionContext.Canvas.DrawPaint(paint);
        executionContext.Canvas.Restore();
    }

    protected override void Dispose(bool disposing) => _context.Dispose();
}
