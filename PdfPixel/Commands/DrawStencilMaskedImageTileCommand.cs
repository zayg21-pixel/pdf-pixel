using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

internal sealed class DrawStencilMaskedImageTileCommand : PdfCommand
{
    private readonly StencilMaskedImageExecutionContext _context;

    public DrawStencilMaskedImageTileCommand(StencilMaskedImageExecutionContext context) => _context = context;

    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale | PdfCommandFeatures.DeferredDispose;

    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        _context.ImageCache.InitializeNextTile(executionContext.ExecutionObserver);
        _context.MaskCache.InitializeNextTile(executionContext.ExecutionObserver);
    }

    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        PdfImageTile imageTile = _context.ImageCache.GetNextTile();
        PdfImageTile maskTile = _context.MaskCache.GetNextTile();
        if (imageTile.IsSkipped || maskTile.IsSkipped || imageTile.Image == null || maskTile.Image == null)
        {
            return;
        }

        SKMatrix ctm = CommandHelpers.GetScaledMatrix(executionContext);
        SKSamplingOptions sampling = PdfImageCommandUtilities.GetSamplingOptions(ctm, _context.ImageSize, interpolate: false); // TODO: [HIGH] wire interpolate

        using SKShader imageShader = ImageBlending.BuildImageShader(imageTile.Image, imageTile.SourceRegion, new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height), sampling);
        using SKShader maskShader = ImageBlending.BuildImageShader(
            maskTile.Image,
            maskTile.SourceRegion,
            new SKSizeI(imageTile.TilePosition.Width, imageTile.TilePosition.Height),
            new SKSamplingOptions(SKFilterMode.Linear));
        using SKShader blendingShader = ImageBlending.CreateStencilMaskShader(imageShader, maskShader, inverse: _context.InvertMask);
        using SKPaint paint = PdfImageCommandUtilities.GetBaseImagePaint(blendingShader, _context.DecodingContext);
        CommandHelpers.ApplyModifiers(paint, modifiers);

        bool antialias = PdfImageCommandUtilities.GetImageTileIsAntialias(ctm, _context.ImageSize, executionContext);

        executionContext.Canvas.Save();
        executionContext.Canvas.Scale(1f / _context.ImageSize.Width, 1f / _context.ImageSize.Height);
        executionContext.Canvas.ClipRect(imageTile.TilePosition, antialias: antialias);
        executionContext.Canvas.Translate(imageTile.TilePosition.Left, imageTile.TilePosition.Top);
        executionContext.Canvas.DrawPaint(paint);
        executionContext.Canvas.Restore();
    }

    protected override void Dispose(bool disposing) => _context.Dispose();
}
