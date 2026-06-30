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
    private readonly SKPath _sourcePath;
    private readonly SKPath _path;
    private readonly SKPath _clipPath;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;
    private readonly SKPaint _basePaint;

    public PathStrokeRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _sourcePath = path;
        _state = state;
        _basePaint = PdfPaintFactory.CreateStrokePaint(state);

        SKPath? fillPath = _basePaint.GetFillPath(path);
        _clipPath = fillPath ?? path;
        _path = (state.StrokePaint.IsPattern) ? _clipPath : path;

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
        }
    }

    public SKRect Bounds => _clipPath.Bounds;

    public SKColor Color => _state.StrokePaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipPathCommand(new SKPath(_clipPath), SKClipOperation.Intersect));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor) => processor.Process(RestoreStateCommand.Instance);

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
        }
        else
        {
            processor.Process(new DrawPathCommand(new SKPath(_path), _basePaint.Clone()));
        }
    }

    public void Dispose()
    {
        _sourcePath.Dispose();
        _clipPath.Dispose();
        _basePaint.Dispose();
    }
}
