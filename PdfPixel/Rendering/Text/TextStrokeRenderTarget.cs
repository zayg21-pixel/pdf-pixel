using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Text render target for stroking text, supporting pattern strokes.
/// </summary>
internal class TextStrokeRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public TextStrokeRenderTarget(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _path = TextRenderUtilities.GetTextPath(shapingResult, state);
        _state = state;

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
        }

        Bounds = (_pattern != null) ? _path.GetStrokeBounds(state.StrokePaint) : PdfRectangle.Empty;
    }

    public PdfRectangle Bounds { get; }

    public PdfColor Color => _state.StrokePaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(SaveStateCommand.Instance);

        if (_pattern != null)
        {
            processor.Process(new ClipPathCommand(_path, PdfClipOperation.Intersect, _state.StrokePaint));
            processor.Process(new SaveLayerCommand(Bounds));
        }
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
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
            processor.Process(new DrawPathCommand(_path, _state.StrokePaint));
        }
    }
}
