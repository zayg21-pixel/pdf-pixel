using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for filling a path, supporting pattern fills.
/// </summary>
internal class PathFillRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public PathFillRenderTarget(PdfPath path, PdfGraphicsState state)
    {
        _path = path;
        _state = state;
        Bounds = path.GetBounds();

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    public PdfRectangle Bounds { get; }

    public PdfColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor, PdfRectangle? patternBounds)
    {
        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ClipPathCommand(_path, PdfClipOperation.Intersect));
        PdfRectangle layerBounds = (patternBounds != null) ? PdfRectangle.Intersect(Bounds, patternBounds.Value) : Bounds;
        processor.Process(new SaveLayerCommand(layerBounds));
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
            processor.Process(new DrawPathCommand(_path, _state.FillPaint));
        }
    }
}
