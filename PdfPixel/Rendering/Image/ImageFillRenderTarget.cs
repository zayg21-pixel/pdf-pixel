using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using System;

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
        _context = new ImageDecodingContext(image, state);
        _loggerFactory = loggerFactory;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    // The image occupies the unit square in current CTM space.
    public PdfRectangle Bounds => new(0, 0, 1, 1);

    public PdfColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        PdfPaint layerPaint = PdfPaintFactory.CreateCompositionLayerPaint(_state);
        processor.Process(new SaveLayerCommand(Bounds, layerPaint));
        processor.Process(new ClipRectangleCommand(Bounds, PdfClipOperation.Intersect));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        if (_image.AlphaMode == PdfImageAlphaMode.StencilMask)
        {
            ImageDecodingContext maskContext = new(_context, _image, PdfColors.White, 1f, PdfBlendMode.MaskComposite);
            processor.Process(SaveStateCommand.Instance);
            ProcessTileCommands(processor, _image, maskContext);
            processor.Process(RestoreStateCommand.Instance);
        }

        processor.Process(RestoreLayerCommand.Instance);
    }

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
            return;
        }

        ProcessTileCommands(processor, _image, _context);
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
                    processor.Process(new DrawStencilMaskImageTileCommand(ctx, i));
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
                    processor.Process(new DrawSoftMaskImageTileCommand(ctx, i));
                }

                break;
            }
            case PdfImageAlphaMode.ImageWithStencilMask:
            {
                PdfImage? stencilMask = image.StencilMask;
                if (stencilMask == null)
                {
                    throw new ArgumentException($"Stencil mask not defined for image {image.SourceReference}.");
                }

                ImageDecodingContext imageLayerContext = new(context, image, PdfColors.White, 1f, PdfBlendMode.Normal);
                NormalImageExecutionContext imageCtx = NormalImageExecutionContext.Create(image, imageLayerContext, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(imageCtx.TileCache, imageCtx.ImageSize));

                ImageDecodingContext maskContext = new(context, stencilMask, PdfColors.White, 1f, PdfBlendMode.MaskComposite);
                StencilMaskImageExecutionContext maskCtx = StencilMaskImageExecutionContext.Create(stencilMask, maskContext, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(maskCtx.TileCache, maskCtx.ImageSize));

                PdfPaint layerPaint = PdfPaintFactory.CreateCompositionLayerPaint(_state);
                processor.Process(new SaveLayerCommand(Bounds, layerPaint));

                for (int i = 0; i < imageCtx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawNormalImageTileCommand(imageCtx, i));
                }

                for (int i = 0; i < maskCtx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawStencilMaskImageTileCommand(maskCtx, i));
                }

                processor.Process(RestoreLayerCommand.Instance);

                break;
            }
            default:
            {
                NormalImageExecutionContext ctx = NormalImageExecutionContext.Create(image, context, _loggerFactory);
                processor.Process(new InitializeTileCacheCommand(ctx.TileCache, ctx.ImageSize));
                for (int i = 0; i < ctx.TileInfo.TotalTiles; i++)
                {
                    processor.Process(new DrawNormalImageTileCommand(ctx, i));
                }

                break;
            }
        }
    }
}
