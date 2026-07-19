using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using SkiaSharp;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for filling a path, supporting pattern fills.
/// </summary>
internal class PathFillRenderTarget : IRenderTarget
{
    private readonly SKPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public PathFillRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    public SKRect Bounds => _path.Bounds;

    public PdfColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipPathCommand(new SKPath(_path), SKClipOperation.Intersect));
        processor.Process(new SaveLayerCommand(_path.Bounds, null));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(RestoreLayerCommand.Instance);
        processor.Process(RestoreStateCommand.Instance);
    }

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
        }
        else
        {
            SKPaint paint = PdfPaintFactory.CreateFillPaint(_state);
            processor.Process(new DrawPathCommand(new SKPath(_path), paint));
        }
    }

    public void Dispose() => _path.Dispose();
}
