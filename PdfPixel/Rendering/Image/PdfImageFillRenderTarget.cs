using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Image;

internal class PdfImageFillRenderTarget : IRenderTarget
{
    private static readonly PdfPaint ShapeMaskFill = PdfPaintFactory.CreateShapeMaskPaint();

    private readonly PdfImage _image;
    private readonly PdfGraphicsState _state;
    private readonly ImageDecodingContext _context;
    private readonly PdfPattern? _pattern;
    private readonly ILoggerFactory _loggerFactory;

    public PdfImageFillRenderTarget(PdfImage image, PdfGraphicsState state, ILoggerFactory loggerFactory)
    {
        _image = image;
        _state = state;
        _context = new ImageDecodingContext(image, state);
        _loggerFactory = loggerFactory;

        if (image.HasImageMask && state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    // The image occupies the unit square in current CTM space.
    public PdfRectangle Bounds => new(0, 0, 1, 1);

    public PdfColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor, PdfRectangle? patternBounds)
    {
        PdfPaint layerPaint = PdfPaintFactory.CreateCompositionLayerPaint(_state);
        processor.Process(new SaveLayerCommand(Bounds, layerPaint));
        processor.Process(new ClipRectangleCommand(Bounds, PdfClipOperation.Intersect));
        processor.Process(new ClipRectangleCommand(null, PdfClipOperation.Intersect, PdfClipRectangleSource.Region));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        if (_image.HasImageMask)
        {
            ImageDecodingContext maskContext = new(_context, _image, ShapeMaskFill);
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
        PdfImageTileCacheEntry tileCache = PdfImageTileCacheEntry.Create(image, context, _loggerFactory);
        for (int tileIndex = 0; tileIndex < tileCache.TileInfo.TotalTiles; tileIndex++)
        {
            processor.Process(new DrawImageTileCommand(tileCache, tileIndex));
        }
    }
}
