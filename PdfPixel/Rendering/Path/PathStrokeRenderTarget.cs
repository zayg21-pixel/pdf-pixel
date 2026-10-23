using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for stroking a path, supporting pattern strokes.
/// </summary>
internal class PathStrokeRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;
    private PdfRectangle _layerBounds;

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

    public void BeforePatternRender(IPdfCommandProcessor processor, PdfRectangle? patternBounds)
    {
        _layerBounds = (patternBounds != null) ? PdfRectangle.Intersect(Bounds, patternBounds.Value) : Bounds;
        processor.Process(new SaveLayerCommand(_layerBounds));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        // The pattern is kept within the stroke by masking it with the stroke drawn as a stroke, instead of clipping it to
        // the stroke's outline.
        processor.Process(new SaveLayerCommand(_layerBounds, PdfPaintFactory.CreateShapeMaskPaint()));
        processor.Process(new DrawPathCommand(_path, PdfPaintFactory.CreateMaskShapePaint(_state.StrokePaint)));
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
            processor.Process(new DrawPathCommand(_path, _state.StrokePaint));
        }
    }
}
