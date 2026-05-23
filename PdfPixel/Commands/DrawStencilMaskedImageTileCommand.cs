using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

internal sealed class DrawStencilMaskedImageTileCommand : PdfCommand
{
    private readonly StencilMaskedImageExecutionContext _context;

    public DrawStencilMaskedImageTileCommand(StencilMaskedImageExecutionContext context)
    {
        _context = context;
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile imageTile = _context.ImageCache.GetNextTile(executionContext.CancellationToken);
        PdfImageTile maskTile = _context.MaskCache.GetNextTile(executionContext.CancellationToken);
        if (imageTile.IsSkipped || maskTile.IsSkipped) return;

        var ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);
        var sampling = PdfImageCommandUtilities.GetSamplingOptions(ctm, _context.DecodingContext, _context.ImageSize, interpolate: false);

        canvas.Save();
        canvas.Scale(1f / _context.ImageSize.Width, 1f / _context.ImageSize.Height);
        canvas.ClipRect(imageTile.TilePosition);
        canvas.Translate(imageTile.TilePosition.Left, imageTile.TilePosition.Top);

        using SKShader imageShader = ImageBlending.BuildImageShader(imageTile.Image, new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height), sampling);
        using SKShader maskShader = ImageBlending.BuildImageShader(maskTile.Image, new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height), new SKSamplingOptions(SKFilterMode.Linear));
        using SKShader blendingShader = ImageBlending.CreateStencilMaskShader(imageShader, maskShader);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, modifiers);
        canvas.DrawPaint(paint);

        canvas.Restore();
    }
}
