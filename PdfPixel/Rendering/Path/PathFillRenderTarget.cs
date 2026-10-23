using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Render target for filling a path, supporting pattern fills.
/// </summary>
internal class PathFillRenderTarget : IRenderTarget
{
    private readonly PdfPath _path;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;
    private PdfRectangle _layerBounds;

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
        _layerBounds = (patternBounds != null) ? PdfRectangle.Intersect(Bounds, patternBounds.Value) : Bounds;
        processor.Process(new SaveLayerCommand(_layerBounds));
    }

    public void AfterPatternRender(IPdfCommandProcessor processor)
    {
        // The pattern is kept within the path by masking it with the path's shape instead of clipping it to the path.
        processor.Process(new SaveLayerCommand(_layerBounds, PdfPaintFactory.CreateShapeMaskPaint()));
        processor.Process(new DrawPathCommand(_path, PdfPaintFactory.CreateMaskShapePaint(_state.FillPaint)));
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
            processor.Process(new DrawPathCommand(_path, _state.FillPaint));
        }
    }
}
