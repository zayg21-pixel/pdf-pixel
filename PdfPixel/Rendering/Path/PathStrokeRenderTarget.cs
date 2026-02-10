using PdfPixel.Color.Paint;
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
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern _pattern;
    private readonly SKPaint _basePaint;

    public PathStrokeRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;
        _basePaint = PdfPaintFactory.CreateStrokePaint(state);

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
            _path = _basePaint.GetFillPath(path);
        }
    }

    public SKPath ClipPath => _path;

    public SKColor Color => _state.StrokePaint.Color;

    public void Render(SKCanvas canvas)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(canvas, _state, this);
        }
        else
        {
            canvas.DrawPath(_path, _basePaint);
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