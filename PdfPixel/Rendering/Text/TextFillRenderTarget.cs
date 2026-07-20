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
/// Text render target for filling text, supporting pattern fills.
/// </summary>
internal class TextFillRenderTarget : IRenderTarget
{
    private readonly SKFont _font;
    private readonly List<ShapedGlyph> _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;
    private readonly SKPath? _clipPath;

    public TextFillRenderTarget(SKFont font, List<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        _font = font;
        _shapingResult = shapingResult;
        _state = state;

        if (state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
            _clipPath = TextRenderUtilities.GetTextPath(shapingResult, font, state);
        }
    }

    public PdfRectangle Bounds => (_clipPath == null) ? PdfRectangle.Empty : new(_clipPath.Bounds.Left, _clipPath.Bounds.Top, _clipPath.Bounds.Right, _clipPath.Bounds.Bottom);

    public PdfColor Color => _state.FillPaint.Color;

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
            PdfMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(_state);

            processor.Process(new DrawShapedTextCommand(textMatrix, _shapingResult.ToArray(), PdfPaintFactory.CloneFont(_font), _state.FillPaint));
        }
    }

    public void Dispose() => _clipPath?.Dispose();
}
