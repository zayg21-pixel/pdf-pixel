using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern;
using PdfPixel.Text;
using System;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Text render target for filling text, supporting pattern fills.
/// </summary>
internal class TextFillRenderTarget : IRenderTarget
{
    private readonly ReadOnlyMemory<ShapedGlyph> _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public TextFillRenderTarget(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _shapingResult = shapingResult;
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
            //PdfPath textPath = PdfTextRenderUtilities.GetTextPath(_shapingResult, _state);
            //processor.Process(new DrawPathCommand(textPath, _state.FillPaint));

            PdfMatrix textMatrix = PdfTextRenderUtilities.GetFullTextMatrix(_state);
            processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult, _state.FillPaint));
        }
    }

    private void RenderPattern(IPdfCommandProcessor processor, PdfPattern pattern)
    {
        PdfGroupSource? source = pattern.GetGroupSource(_state, _state.FillPaint);

        if (source != null)
        {
            PdfMatrix textMatrix = PdfTextRenderUtilities.GetFullTextMatrix(_state);
            processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult, _state.FillPaint, source));
        }
    }
}
