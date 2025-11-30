using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Model;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using PdfReader.Rendering.Text;
using PdfReader.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Text;

/// <summary>
/// Manages text drawing with proper selection and positioning.
/// </summary>
public class PdfTextRenderer : IPdfTextRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILogger<PdfTextRenderer> _logger;

    internal PdfTextRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        _logger = loggerFactory.CreateLogger<PdfTextRenderer>();
    }

    /// <inheritdoc/>
    public float DrawTextSequence(SKCanvas canvas, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {

        float width = 0f;

        if (glyphs.Count > 0)
        {
            using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, state);

            softMaskScope.BeginDrawContent();

            using var skFont = font.GetSkiaFont();

            width = DrawShapedText(canvas, skFont, glyphs, state);

            softMaskScope.EndDrawContent();
        }

        return width;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float DrawShapedText(SKCanvas canvas, SKFont font, List<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        if (shapingResult.Count == 0)
        {
            return 0;
        }

        // this produces the combined text matrix with rise and font size applied, needed to draw outlines correctly
        var textMatrix = state.TextMatrix;

        if (state.Rise != 0)
        {
            textMatrix = SKMatrix.Concat(textMatrix, SKMatrix.CreateTranslation(0, state.Rise));
        }

        // Apply font size, horizontal scaling, and vertical flip
        var fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
        var fontScalingMatrix = SKMatrix.CreateScale(fullHorizontalScale, -state.FontSize);
        textMatrix = SKMatrix.Concat(textMatrix, fontScalingMatrix);

        if (ShouldFill(state.TextRenderingMode))
        {
            using var textFillTarget = new TextFillRenderTarget(font, shapingResult, state);
            textFillTarget.Render(canvas);
        }

        if (ShouldStroke(state.TextRenderingMode))
        {
            using var textStrokeTarget = new TextStrokeRenderTarget(font, shapingResult, state);
            textStrokeTarget.Render(canvas);
        }

        // Apply clipping if requested (modes with Clip). Pure clip mode skips drawing above.
        if (ShouldClip(state.TextRenderingMode))
        {
            using var textPath = TextRenderUtilities.GetTextPath(shapingResult, font, state);
            if (!textPath.IsEmpty)
            {
                state.TextClipPath ??= new SKPath();
                state.TextClipPath.AddPath(textPath);
            }
        }

        return TextRenderUtilities.GetTextWidth(shapingResult) * fullHorizontalScale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldFill(PdfTextRenderingMode mode)
    {
        switch (mode)
        {
            case PdfTextRenderingMode.Fill:
            case PdfTextRenderingMode.FillAndStroke:
            case PdfTextRenderingMode.FillAndClip:
            case PdfTextRenderingMode.FillAndStrokeAndClip:
                return true;
            default:
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldStroke(PdfTextRenderingMode mode)
    {
        switch (mode)
        {
            case PdfTextRenderingMode.Stroke:
            case PdfTextRenderingMode.FillAndStroke:
            case PdfTextRenderingMode.StrokeAndClip:
            case PdfTextRenderingMode.FillAndStrokeAndClip:
                return true;
            default:
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldClip(PdfTextRenderingMode mode)
    {
        switch (mode)
        {
            case PdfTextRenderingMode.Clip:
            case PdfTextRenderingMode.FillAndClip:
            case PdfTextRenderingMode.StrokeAndClip:
            case PdfTextRenderingMode.FillAndStrokeAndClip:
                return true;
            default:
                return false;
        }
    }
}