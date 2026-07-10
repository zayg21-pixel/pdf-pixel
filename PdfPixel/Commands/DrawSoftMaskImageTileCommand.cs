using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Draws one tile of an image combined with a soft mask image (/SMask), blending the image through the grayscale mask onto the canvas.
/// </summary>
public sealed class DrawSoftMaskImageTileCommand : PdfCommand
{
    private readonly SoftMaskImageExecutionContext _context;

    internal DrawSoftMaskImageTileCommand(SoftMaskImageExecutionContext context) => _context = context;

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        PdfImageTile imageTile = _context.ImageCache.GetNextTile(executionContext.ExecutionObserver);
        PdfImageTile maskTile = _context.MaskCache.GetNextTile(executionContext.ExecutionObserver);

        if (imageTile.Image == null || maskTile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            executionContext, _context.ImageSize, imageTile.TilePosition, _context.Interpolate);

        SKColor? matte = null;
        if (_context.MatteArray != null && maskTile.Parameters != null)
        {
            matte = maskTile.Parameters.ColorSpaceConverter.ToSrgb(_context.MatteArray, maskTile.Parameters.RenderingIntent, default);
        }

        using SKShader imageShader = ImageBlending.BuildImageShader(imageTile.Image, placement.DeviceSize, placement.Sampling);
        using SKShader maskShader = ImageBlending.BuildImageShader(maskTile.Image, placement.DeviceSize, placement.Sampling);
        using SKShader blendingShader = ImageBlending.CreateSoftMaskBlendingShader(imageShader, maskShader, matte);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, executionContext);

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(placement.PlacementMatrix);
        executionContext.Canvas.ClipRect(placement.PlacementRectangle);
        executionContext.Canvas.DrawPaint(paint);
        executionContext.Canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _context.Dispose();
}
