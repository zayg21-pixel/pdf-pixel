using PdfPixel.Color;
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
    private PdfRectangle _layerBounds;

    public TextFillRenderTarget(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _shapingResult = shapingResult;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }

        Bounds = TextRenderUtilities.GetTextBounds(shapingResult.Span, state) ?? state.GetUserSpaceClipBounds();
    }

    public PdfRectangle Bounds { get; }

    public PdfColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor, PdfRectangle? patternBounds)
    {
        _layerBounds = (patternBounds != null) ? PdfRectangle.Intersect(Bounds, patternBounds.Value) : Bounds;
        processor.Process(new SaveLayerCommand(_layerBounds));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        // The pattern is kept within the glyphs by masking it with the text drawn as text, as a solid fill draws it,
        // instead of clipping it to an outline built from the glyphs.
        PdfMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(_state);
        processor.Process(new SaveLayerCommand(_layerBounds, PdfPaintFactory.CreateShapeMaskPaint()));
        processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult, PdfPaintFactory.CreateMaskShapePaint(_state.FillPaint)));
        processor.Process(RestoreLayerCommand.Instance);
        processor.Process(RestoreLayerCommand.Instance);
    }

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
        }
        else
        {
            //PdfPath textPath = TextRenderUtilities.GetTextPath(_shapingResult, _state);
            //processor.Process(new DrawPathCommand(textPath, _state.FillPaint));

            PdfMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(_state);
            processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult, _state.FillPaint));
        }
    }
}
