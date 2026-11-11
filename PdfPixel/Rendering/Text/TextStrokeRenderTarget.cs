using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern;
using PdfPixel.Text;
using System;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Text render target for stroking text, supporting pattern strokes.
/// </summary>
internal class TextStrokeRenderTarget : IRenderTarget
{
    private readonly ReadOnlyMemory<ShapedGlyph> _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public TextStrokeRenderTarget(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _shapingResult = shapingResult;
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
            PdfMatrix textMatrix = PdfTextRenderUtilities.GetFullTextMatrix(_state);
            processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult, _state.StrokePaint));
        }
    }

    private void RenderPattern(IPdfCommandProcessor processor, PdfPattern pattern)
    {
        PdfGroupSource? source = pattern.GetGroupSource(_state, _state.StrokePaint);

        if (source != null)
        {
            PdfMatrix textMatrix = PdfTextRenderUtilities.GetFullTextMatrix(_state);
            processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult, _state.StrokePaint, source));
        }
    }
}
