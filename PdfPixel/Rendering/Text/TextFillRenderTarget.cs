using PdfPixel.Color;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
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
    private readonly PdfPath? _clipPath;

    public TextFillRenderTarget(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _shapingResult = shapingResult;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
            _clipPath = TextRenderUtilities.GetTextPath(shapingResult, state);
        }

        Bounds = _clipPath?.GetBounds() ?? PdfRectangle.Empty;
    }

    public PdfRectangle Bounds { get; }

    public PdfColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor, PdfRectangle? patternBounds)
    {
        processor.Process(SaveStateCommand.Instance);
        if (_clipPath != null)
        {
            processor.Process(new ClipPathCommand(_clipPath, PdfClipOperation.Intersect));
            PdfRectangle layerBounds = (patternBounds != null) ? PdfRectangle.Intersect(Bounds, patternBounds.Value) : Bounds;
            processor.Process(new SaveLayerCommand(layerBounds));
        }
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        if (_clipPath != null)
        {
            processor.Process(RestoreLayerCommand.Instance);
        }

        processor.Process(RestoreStateCommand.Instance);
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
