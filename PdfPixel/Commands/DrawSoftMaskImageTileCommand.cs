using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

internal sealed class DrawSoftMaskImageTileCommand : PdfCommand
{
    private readonly SoftMaskImageExecutionContext _context;

    public DrawSoftMaskImageTileCommand(SoftMaskImageExecutionContext context) => _context = context;

    public override bool IsScaleDependent => true;

    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile imageTile = _context.ImageCache.GetNextTile(executionContext.ExecutionObserver);
        PdfImageTile maskTile = _context.MaskCache.GetNextTile(executionContext.ExecutionObserver);
        if (imageTile.IsSkipped || maskTile.IsSkipped)
        {
            return;
        }

        if (imageTile.Image == null || maskTile.Image == null)
        {
            return;
        }

        SKMatrix ctm = CommandHelpers.GetScaledMatrix(executionContext);
        SKSamplingOptions sampling = PdfImageCommandUtilities.GetSamplingOptions(ctm, _context.ImageSize, _context.Interpolate);

        SKColor? matte = null;
        if (_context.MatteArray != null && maskTile.Parameters != null)
        {
            matte = maskTile.Parameters.ColorSpaceConverter.ToSrgb(_context.MatteArray, maskTile.Parameters.RenderingIntent, default);
        }

        using SKShader imageShader = ImageBlending.BuildImageShader(imageTile.Image, new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height), sampling);
        using SKShader maskShader = ImageBlending.BuildImageShader(maskTile.Image, new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height), sampling);
        using SKShader blendingShader = ImageBlending.CreateSoftMaskBlendingShader(imageShader, maskShader, matte);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, modifiers);

        executionContext.Canvas.Save();
        executionContext.Canvas.Scale(1f / _context.ImageSize.Width, 1f / _context.ImageSize.Height);
        executionContext.Canvas.ClipRect(imageTile.TilePosition);
        executionContext.Canvas.Translate(imageTile.TilePosition.Left, imageTile.TilePosition.Top);
        executionContext.Canvas.DrawPaint(paint);
        executionContext.Canvas.Restore();
    }

    protected override void Dispose(bool disposing) => _context.Dispose();
}
