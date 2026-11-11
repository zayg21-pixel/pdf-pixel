using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for filling a path, supporting pattern fills.
/// </summary>
internal class PathFillRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public PathFillRenderTarget(PdfPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    public PdfPaint Paint => _state.FillPaint;

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            RenderPattern(processor, _pattern);
        }
        else
        {
            processor.Process(new DrawPathCommand(_path, _state.FillPaint));
        }
    }

    private void RenderPattern(IPdfCommandProcessor processor, PdfPattern pattern)
    {
        PdfGroupSource? source = pattern.GetGroupSource(_state, _state.FillPaint);

        if (source != null)
        {
            processor.Process(new DrawPathCommand(_path, _state.FillPaint, source));
        }
    }
}
