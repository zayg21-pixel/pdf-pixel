using PdfPixel.Color.Paint;
using PdfPixel.Commands;
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
    private readonly IList<ShapedGlyph> _shapingResult;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;
    private readonly SKPath? _clipPath;

    public TextFillRenderTarget(SKFont font, IList<ShapedGlyph> shapingResult, PdfGraphicsState state)
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

    public SKRect Bounds => _clipPath?.Bounds ?? SKRect.Empty;

    public SKColor Color => _state.FillPaint.Color;

    public void BeforePatternRender(IPdfCommandProcessor processor)
    {
        processor.Process(new SaveStateCommand());
        if (_clipPath != null)
        {
            processor.Process(new ClipPathCommand(_clipPath, SKClipOperation.Intersect));
        }
    }

    public void AfterPatternRender(IPdfCommandProcessor processor) => processor.Process(new RestoreStateCommand());

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            _pattern.RenderPattern(processor, _state, this);
        }
        else
        {
            //SKPaint paint = PdfPaintFactory.CreateFillPaint(_state);
            //SKPath path = TextRenderUtilities.GetTextPath(_shapingResult, _font, _state);
            //processor.Process(new DrawPathCommand(path, paint));

            SKMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(_state);

            processor.Process(new SaveStateCommand());

            // Apply text matrix transformation
            processor.Process(new ConcatMatrixCommand(textMatrix));

            SKTextBlob? blob = TextRenderUtilities.BuildTextBlob(_shapingResult, _font);

            if (blob != null)
            {
                SKPaint paint = PdfPaintFactory.CreateFillPaint(_state);
                processor.Process(new DrawTextBlobCommand(blob, paint));
            }

            processor.Process(new RestoreStateCommand());
        }
    }

    public void Dispose() => _clipPath?.Dispose();
}
