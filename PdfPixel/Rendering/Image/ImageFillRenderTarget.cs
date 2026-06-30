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
    private readonly PdfPattern? _pattern;
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
        SKPaint layerPaint = new()
        {
            BlendMode = _context.BlendMode,
            Color = PdfPaintFactory.ApplyAlpha(SKColors.White, _context.FillAlpha)
        };
        processor.Process(new SaveLayerCommand(new SKRect(0, 0, 1, 1), layerPaint));
        processor.Process(new ClipPathCommand(new SKRect(0, 0, 1, 1), SKClipOperation.Intersect));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        if (_image.AlphaMode == PdfImageAlphaMode.StencilMask)
        {
            ImageDecodingContext maskContext = new(_context, SKColors.White, 1f, SKBlendMode.DstIn);
            processor.Process(SaveStateCommand.Instance);
            processor.Process(new ConcatMatrixCommand(PdfImageCommandUtilities.GetImageMatrix()));
            ProcessTileCommands(processor, _image, maskContext);
            processor.Process(RestoreStateCommand.Instance);
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
            return;
        }

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipPathCommand(new SKRect(0, 0, 1, 1), SKClipOperation.Intersect));
        processor.Process(new ConcatMatrixCommand(PdfImageCommandUtilities.GetImageMatrix()));

        ProcessTileCommands(processor, _image, _context);

        processor.Process(RestoreStateCommand.Instance);
    }

    private void ProcessTileCommands(IPdfCommandProcessor processor, PdfImage image, ImageDecodingContext context)
    {
        switch (image.AlphaMode)
        {
            case PdfImageAlphaMode.StencilMask:
            {
                StencilMaskImageExecutionContext ctx = StencilMaskImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.TileCache, ctx.ImageSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawStencilMaskImageTileCommand(ctx));
                }

                break;
            }
            case PdfImageAlphaMode.ImageWithSoftAlphaMask:
            {
                SoftMaskImageExecutionContext ctx = SoftMaskImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.ImageCache, ctx.ImageSize));
                processor.Process(new InitializeTileCacheCommand(ctx.MaskCache, ctx.MaskSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawSoftMaskImageTileCommand(ctx));
                }

                break;
            }
            case PdfImageAlphaMode.ImageWithStencilMask:
            {
                StencilMaskedImageExecutionContext ctx = StencilMaskedImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.ImageCache, ctx.ImageSize));
                processor.Process(new InitializeTileCacheCommand(ctx.MaskCache, ctx.MaskSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawStencilMaskedImageTileCommand(ctx));
                }

                break;
            }
            default:
            {
                NormalImageExecutionContext ctx = NormalImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.TileCache, ctx.ImageSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawNormalImageTileCommand(ctx));
                }

                break;
            }
        }
    }

    public void Dispose()
    {
    }
}
