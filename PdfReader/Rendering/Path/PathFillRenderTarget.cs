using PdfReader.Color.Paint;
using PdfReader.Pattern.Model;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Rendering.Path;

/// <summary>
/// Render target for filling a path, supporting pattern fills.
/// </summary>
internal class PathFillRenderTarget : IRenderTarget
{
    private readonly SKPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern _pattern;

    public PathFillRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    public SKPath ClipPath => _path;

    public SKColor Color => _state.FillPaint.Color;

    public void Render(SKCanvas canvas)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(canvas, _state, this);
        }
        else
        {
            using var paint = PdfPaintFactory.CreateFillPaint(_state);
            canvas.DrawPath(_path, paint);
        }
    }

    public void Dispose()
    {
    }
}
