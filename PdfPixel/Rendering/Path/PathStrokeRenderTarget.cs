using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for stroking a path, supporting pattern strokes.
/// </summary>
internal class PathStrokeRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public PathStrokeRenderTarget(PdfPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
        }
    }

    public PdfPaint Paint => _state.StrokePaint;

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            RenderPattern(processor, _pattern);
        }
        else
        {
            processor.Process(new DrawPathCommand(_path, _state.StrokePaint));
        }
    }

    private void RenderPattern(IPdfCommandProcessor processor, PdfPattern pattern)
    {
        PdfGroupSource? source = pattern.GetGroupSource(_state, _state.StrokePaint);

        if (source != null)
        {
            processor.Process(new DrawPathCommand(_path, _state.StrokePaint, source));
        }
    }
}
