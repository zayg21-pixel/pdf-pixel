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

    public SKColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(new SaveStateCommand());
        processor.Process(new ClipPathCommand(_path, SKClipOperation.Intersect));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor) => processor.Process(new RestoreStateCommand());

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
        }
        else
        {
            SKPaint paint = PdfPaintFactory.CreateFillPaint(_state);
            processor.Process(new DrawPathCommand(_path, paint));
        }
    }

    public void Dispose()
    {
    }
}
