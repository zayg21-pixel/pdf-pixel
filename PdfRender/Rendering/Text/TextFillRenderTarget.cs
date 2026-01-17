using PdfRender.Color.Paint;
using PdfRender.Pattern.Model;
using PdfRender.Rendering.State;
using PdfRender.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfRender.Rendering.Text;

/// <summary>
/// Text render target for filling text, supporting pattern fills.
/// </summary>
internal class TextFillRenderTarget : IRenderTarget
{
    private readonly SKFont _font;
    private readonly IList<ShapedGlyph> _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern _pattern;
    private SKPath _clipPath;

    public TextFillRenderTarget(SKFont font, IList<ShapedGlyph> shapingResult, PdfGraphicsState state)
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
            //using var paint = PdfPaintFactory.CreateFillPaint(_state);
            //using var path = TextRenderUtilities.GetTextPath(_shapingResult, _font, _state);
            //canvas.DrawPath(path, paint);

            var textMatrix = TextRenderUtilities.GetFullTextMatrix(_state);

            canvas.Save();

            // Apply text matrix transformation
            canvas.Concat(textMatrix);

            using var blob = TextRenderUtilities.BuildTextBlob(_shapingResult, _font);

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
