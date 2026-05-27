using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

internal sealed class DrawStencilMaskImageTileCommand : PdfCommand
{
    private readonly StencilMaskImageExecutionContext _context;

    public DrawStencilMaskImageTileCommand(StencilMaskImageExecutionContext context) => _context = context;

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile tile = _context.TileCache.GetNextTile(executionContext.ExecutionObserver);
        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        canvas.Save();
        canvas.Scale(1f / _context.ImageSize.Width, 1f / _context.ImageSize.Height);
        canvas.ClipRect(tile.TilePosition);
        canvas.Translate(tile.TilePosition.Left, tile.TilePosition.Top);

        using SKShader stencilShader = ImageBlending.BuildImageShader(tile.Image, new SKSizeI(tile.TilePosition.Width, tile.TilePosition.Height), new SKSamplingOptions(SKFilterMode.Nearest));
        using SKShader blendingShader = ImageBlending.CreateImageMaskBlendingShader(stencilShader, _context.DecodingContext.FillColor, inverse: true);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, modifiers);
        canvas.DrawPaint(paint);

        canvas.Restore();
    }

    protected override void Dispose(bool disposing) => _context.Dispose();
}
