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
            processor.Process(new DrawStencilMaskCommand(_image, maskContext, _loggerFactory));
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

        PdfCommand drawCommand = _image.AlphaMode switch
        {
            PdfImageAlphaMode.StencilMask => new DrawStencilMaskCommand(_image, _context, _loggerFactory),
            PdfImageAlphaMode.ImageWithSoftAlphaMask => new DrawSoftMaskImageCommand(_image, _context, _loggerFactory),
            PdfImageAlphaMode.ImageWithStencilMask => new DrawStencilMaskedImageCommand(_image, _context, _loggerFactory),
            _ => new DrawNormalImageCommand(_image, _context, _loggerFactory),
        };

        processor.Process(drawCommand);
        processor.Process(new RestoreStateCommand());
    }

    public void Dispose()
    {
    }
}
