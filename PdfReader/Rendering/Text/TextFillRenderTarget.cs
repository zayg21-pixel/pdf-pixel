using PdfReader.Color.Paint;
using PdfReader.Pattern.Model;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Rendering.Text;

/// <summary>
/// Text render target for filling text, supporting pattern fills.
/// </summary>
internal class TextFillRenderTarget : IRenderTarget
{
    private readonly SKFont _font;
    private readonly ShapedGlyph[] _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern _pattern;
    private SKPath _clipPath;

    public TextFillRenderTarget(SKFont font, ShapedGlyph[] shapingResult, PdfGraphicsState state)
    {
        _font = font;
        _shapingResult = shapingResult;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    public SKPath ClipPath
    {
        get
        {
            _clipPath ??= TextRenderUtilities.GetTextPath(_shapingResult, _font, _state);
            return _clipPath;
        }
    }

    public SKColor Color => _state.FillPaint.Color;

    public void Render(SKCanvas canvas)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(canvas, _state, this);
        }
        else
        {
            var textMatrix = TextRenderUtilities.GetFullTextMatrix(_state);

            canvas.Save();

            // Apply text matrix transformation
            canvas.Concat(textMatrix);

            using var blob = TextRenderUtilities.BuldTextBlob(_shapingResult, _font);

            if (blob != null)
            {
                using var paint = PdfPaintFactory.CreateFillPaint(_state);
                canvas.DrawText(blob, 0f, 0f, paint);
            }

            canvas.Restore();
        }
    }

    public void Dispose()
    {
        _clipPath?.Dispose();
    }
}
