using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Text render target for stroking text, supporting pattern strokes.
/// </summary>
internal class TextStrokeRenderTarget : IRenderTarget
{
    private readonly SKFont _font;
    private readonly List<ShapedGlyph> _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly SKPaint _strokePaint;
    private readonly PdfPattern? _pattern;
    private readonly SKPath? _clipPath;

    public TextStrokeRenderTarget(SKFont font, List<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _font = font;
        _shapingResult = shapingResult;
        _state = state;
        // Exception: SKPaint.GetFillPath has no PdfPaint equivalent, so we need the real SKPaint here.
        _strokePaint = state.StrokePaint.ToSkiaPaint();

        if (state.StrokePaint.IsPattern)
        {
            _pattern = state.StrokePaint.Pattern;
            SKPath sourcePath = TextRenderUtilities.GetTextPath(shapingResult, font, state);
            SKPath fillPath = _strokePaint.GetFillPath(sourcePath);
            if (fillPath != null)
            {
                sourcePath.Dispose();
                _clipPath = fillPath;
            }
            else
            {
                _clipPath = sourcePath;
            }
        }
    }

    public PdfRectangle Bounds => (_clipPath == null) ? PdfRectangle.Empty : new(_clipPath.Bounds.Left, _clipPath.Bounds.Top, _clipPath.Bounds.Right, _clipPath.Bounds.Bottom);

    public PdfColor Color => _state.StrokePaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(SaveStateCommand.Instance);

        if (_clipPath != null)
        {
            processor.Process(new ClipPathCommand(new SKPath(_clipPath), SKClipOperation.Intersect));
            processor.Process(new SaveLayerCommand(_clipPath.Bounds, (SKPaint?)null));
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
            SKPath path = TextRenderUtilities.GetTextPath(_shapingResult, _font, _state);
            processor.Process(new DrawPathCommand(path, _strokePaint.Clone()));
        }
    }

    public void Dispose()
    {
        _strokePaint.Dispose();
        _clipPath?.Dispose();
    }
}
