using PdfReader.Color.Paint;
using PdfReader.Pattern.Model;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Rendering.Text;

/// <summary>
/// Text render target for stroking text, supporting pattern strokes.
/// </summary>
internal class TextStrokeRenderTarget : IRenderTarget
{
    private readonly SKFont _font;
    private readonly ShapedGlyph[] _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly SKPaint _strokePaint;
    private readonly PdfPattern _pattern;
    private SKPath _clipPath;

    public TextStrokeRenderTarget(SKFont font, ShapedGlyph[] shapingResult, PdfGraphicsState state)
    {
        _font = font;
        _shapingResult = shapingResult;
        _state = state;
        _strokePaint = PdfPaintFactory.CreateStrokePaint(state);

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
        }
    }

    public SKPath ClipPath
    {
        get
        {
            if (_clipPath == null)
            {
                using var sourcePath = TextRenderUtilities.GetTextPath(_shapingResult, _font, _state);
                _clipPath = _strokePaint.GetFillPath(sourcePath);
            }

            return _clipPath;
        }
    }

    public SKColor Color => _state.StrokePaint.Color;

    public void Render(SKCanvas canvas)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(canvas, _state, this);
        }
        else
        {
            using var path = TextRenderUtilities.GetTextPath(_shapingResult, _font, _state);
            canvas.DrawPath(path, _strokePaint);
        }
    }

    public void Dispose()
    {
        _strokePaint.Dispose();
        _clipPath?.Dispose();
    }
}
