using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for stroking a path, supporting pattern strokes.
/// </summary>
internal class PathStrokeRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public PathStrokeRenderTarget(PdfPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;
        Bounds = path.GetStrokeBounds(state.StrokePaint);

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
        }
    }

    public PdfRectangle Bounds { get; }

    public PdfColor Color => _state.StrokePaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipStrokePathCommand(_path, _state.StrokePaint, PdfClipOperation.Intersect));
        processor.Process(new SaveLayerCommand(Bounds));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(RestoreLayerCommand.Instance);
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
