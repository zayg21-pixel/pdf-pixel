using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using SkiaSharp;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for stroking a path, supporting pattern strokes.
/// </summary>
internal class PathStrokeRenderTarget : IRenderTarget
{
    private readonly SKPath _path;
    private readonly SKPath _clipPath;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;
    private readonly SKPaint _basePaint;

    public PathStrokeRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;
        _basePaint = PdfPaintFactory.CreateStrokePaint(state);
        _clipPath = _basePaint.GetFillPath(path) ?? path;

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
            _path = _clipPath;
        }
    }

    public SKRect Bounds => _clipPath.Bounds;

    public SKColor Color => _state.StrokePaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(new SaveStateCommand());
        processor.Process(new ClipPathCommand(_clipPath, SKClipOperation.Intersect));
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
            processor.Process(new DrawPathCommand(_path, _basePaint.Clone()));
        }
    }

    public void Dispose()
    {
        if (_pattern != null)
        {
            // dispose local generated path.
            _path.Dispose();
        }

        _basePaint.Dispose();

    }
}
