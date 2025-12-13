using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
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
    public SKSize DrawTextSequence(SKCanvas canvas, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, state);
        softMaskScope.BeginDrawContent();

        if (font.SubstituteFont)
        {
            const float ScaleTolerancePercent = 0.01f; // 1%
            var glyphBuffer = new List<ShapedGlyph>();
            SKFont skFont = null;

            for (int i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                var typeface = glyph.CharacterInfo.Typeface;
                var scale = glyph.Scale;

                if (skFont?.Typeface != typeface || Math.Abs(scale - skFont.ScaleX) / skFont.ScaleX >= ScaleTolerancePercent)
                {
                    if (glyphBuffer.Count > 0 && skFont != null)
                    {
                        DrawShapedText(canvas, skFont, glyphBuffer, state);
                    }

                    glyphBuffer.Clear();
                    skFont?.Dispose();

                    skFont = PdfPaintFactory.CreateTextFont(typeface);
                    skFont.ScaleX = scale;
                }

                glyphBuffer.Add(glyph);
            }

            if (glyphBuffer.Count > 0 && skFont != null)
            {
                DrawShapedText(canvas, skFont, glyphBuffer, state);
            }

            skFont?.Dispose();
        }
        else if (glyphs.Count > 0)
        {
            var baseTypeface = glyphs[0].CharacterInfo.Typeface;
            using var skFont = PdfPaintFactory.CreateTextFont(baseTypeface);
            DrawShapedText(canvas, skFont, glyphs, state);
        }

        softMaskScope.EndDrawContent();

        if (state.CurrentFont.WritingMode == Fonts.Mapping.CMapWMode.Vertical)
        {
            return new SKSize(0, TextRenderUtilities.GetTextHeight(glyphs) * state.FontSize);
        }
        else
        {
            // Apply font size, horizontal scaling, and vertical flip
            var fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;

            return new SKSize(TextRenderUtilities.GetTextWidth(glyphs) * fullHorizontalScale, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawShapedText(SKCanvas canvas, SKFont font, IList<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
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