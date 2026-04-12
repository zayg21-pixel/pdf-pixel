using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Rendering.Text;
using PdfPixel.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Text;

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
    public SKSize DrawTextSequence(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        if (font == null || glyphs.Count == 0)
        {
            return SKSize.Empty;
        }

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, processor, state);
        softMaskScope.BeginDrawContent();

        if (font is PdfType3Font type3Font)
        {
            RenderType3(processor, glyphs, state, type3Font);
        }
        else if (font.SubstituteFont)
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
                        DrawShapedText(processor, skFont, glyphBuffer, state);
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
                DrawShapedText(processor, skFont, glyphBuffer, state);
            }

            skFont?.Dispose();
        }
        else if (glyphs.Count > 0)
        {
            var baseTypeface = glyphs[0].CharacterInfo.Typeface;
            using var skFont = PdfPaintFactory.CreateTextFont(baseTypeface);
            DrawShapedText(processor, skFont, glyphs, state);
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
    private void RenderType3(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfType3Font type3Font)
    {
        // Skip rendering for Invisible or Clip modes as per PDF spec.
        if (state.TextRenderingMode == PdfTextRenderingMode.Invisible || state.TextRenderingMode == PdfTextRenderingMode.Clip)
        {
            return;
        }

        // Type3 glyphs are recorded commands in glyph space (after FontMatrix). Apply text matrix and per-glyph offsets.
        processor.Process(new SaveStateCommand());
        var fullTextMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);
        processor.Process(new ConcatMatrixCommand(fullTextMatrix));

        for (int i = 0; i < glyphs.Count; i++)
        {
            var glyph = glyphs[i];
            var charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode, _renderer, state);
            if (charInfo.IsDefined)
            {
                IPdfCommandModifier modifier = charInfo.IsColored
                    ? default
                    : new UncoloredPaintModifier(state.FillPaint.Color);

                processor.Process(new SaveStateCommand());
                // Translate by glyph X/Y (already in text space units after fullTextMatrix).
                processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(glyph.X, glyph.Y)));
                processor.Process(new ConcatMatrixCommand(type3Font.FontMatrix));
                processor.Process(new DrawRecordingCommand(charInfo.Recording, modifier, disposeRecording: false));
                processor.Process(new RestoreStateCommand());

                modifier.Dispose();
            }
        }

        processor.Process(new RestoreStateCommand());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawShapedText(IPdfCommandProcessor processor, SKFont font, IList<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        if (ShouldFill(state.TextRenderingMode))
        {
            using var textFillTarget = new TextFillRenderTarget(font, shapingResult, state);
            textFillTarget.Render(processor);
        }

        if (ShouldStroke(state.TextRenderingMode))
        {
            using var textStrokeTarget = new TextStrokeRenderTarget(font, shapingResult, state);
            textStrokeTarget.Render(processor);
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