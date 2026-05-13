using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Model;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using SkiaSharp;

namespace PdfPixel.Rendering.Image;

internal class ImageFillRenderTarget : IRenderTarget
{
    private readonly PdfImage _image;
    private readonly PdfGraphicsState _state;
    private readonly ImageDecodingContext _context;
    private readonly PdfPattern _pattern;
    private readonly ILoggerFactory _loggerFactory;

    public ImageFillRenderTarget(PdfImage image, PdfGraphicsState state, ILoggerFactory loggerFactory)
    {
        _image = image;
        _state = state;
        _context = new ImageDecodingContext(state);
        _loggerFactory = loggerFactory;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    // The image occupies the unit square in current CTM space.
    public SKRect Bounds => SKRect.Create(0, 0, 1, 1);

    public SKColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        var layerPaint = new SKPaint
        {
            BlendMode = _context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, _context.FillAlpha),
        };
        processor.Process(new SaveLayerCommand(new SKRect(0, 0, 1, 1), layerPaint));
        processor.Process(new ClipPathCommand(new SKRect(0, 0, 1, 1), SKClipOperation.Intersect));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        if (_image.AlphaMode == PdfImageAlphaMode.StencilMask)
        {
            var maskContext = new ImageDecodingContext(_context, SKColors.White, 1f, SKBlendMode.DstIn);
            processor.Process(new SaveStateCommand());
            processor.Process(new ConcatMatrixCommand(PdfImageCommandUtilities.GetImageMatrix()));
            ProcessTileCommands(processor, _image, maskContext);
            processor.Process(new RestoreStateCommand());
        }

        processor.Process(new RestoreStateCommand());
    }

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
            return;
        }

        processor.Process(new SaveStateCommand());
        processor.Process(new ClipPathCommand(new SKRect(0, 0, 1, 1), SKClipOperation.Intersect));
        processor.Process(new ConcatMatrixCommand(PdfImageCommandUtilities.GetImageMatrix()));

        ProcessTileCommands(processor, _image, _context);

        processor.Process(new RestoreStateCommand());
    }

    private void ProcessTileCommands(IPdfCommandProcessor processor, PdfImage image, ImageDecodingContext context)
    {
        switch (image.AlphaMode)
        {
            case PdfImageAlphaMode.StencilMask:
            {
                var ctx = StencilMaskImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.TileCache, ctx.ImageSize, ctx));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                    processor.Process(new DrawStencilMaskImageTileCommand(ctx));
                break;
            }
            case PdfImageAlphaMode.ImageWithSoftAlphaMask:
            {
                var ctx = SoftMaskImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.ImageCache, ctx.ImageSize, ctx));
                processor.Process(new InitializeTileCacheCommand(ctx.MaskCache, ctx.MaskSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                    processor.Process(new DrawSoftMaskImageTileCommand(ctx));
                break;
            }
            case PdfImageAlphaMode.ImageWithStencilMask:
            {
                var ctx = StencilMaskedImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.ImageCache, ctx.ImageSize, ctx));
                processor.Process(new InitializeTileCacheCommand(ctx.MaskCache, ctx.MaskSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                    processor.Process(new DrawStencilMaskedImageTileCommand(ctx));
                break;
            }
            default:
            {
                var ctx = NormalImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.TileCache, ctx.ImageSize, ctx));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                    processor.Process(new DrawNormalImageTileCommand(ctx));
                break;
            }
        }
    }

    public void Dispose()
    {
    }
}
