using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Rendering.Text;
using PdfPixel.TextExtraction;
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
    public PdfSize DrawTextSequence(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase? font)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (glyphs == null)
        {
            throw new ArgumentNullException(nameof(glyphs));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (font == null || glyphs.Count == 0)
        {
            return PdfSize.Empty;
        }

        if (!state.RenderingParameters.RenderText && !state.RenderingParameters.ExtractText)
        {
            return PdfSize.Empty;
        }

        if (state.RenderingParameters.RenderText)
        {
            using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state);
            softMaskScope.BeginDrawContent();
            ProcessGlyphs(processor, glyphs, state, font);
            softMaskScope.EndDrawContent();
        }
        else
        {
            ProcessGlyphs(processor, glyphs, state, font);
        }

        if (state.CurrentFont != null && state.CurrentFont.WritingMode == Fonts.Mapping.CMapWMode.Vertical)
        {
            return new PdfSize(0, -TextRenderUtilities.GetTextHeight(glyphs) * state.FontSize);
        }
        else
        {
            float fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
            return new PdfSize(TextRenderUtilities.GetTextWidth(glyphs) * fullHorizontalScale, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessGlyphs(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        if (font is PdfType3Font type3Font)
        {
            ProcessType3(processor, glyphs, state, type3Font);
        }
        else if (font.IsSubstitutedFont)
        {
            const float scaleTolerancePercent = 0.01f; // 1%
            List<ShapedGlyph> glyphBuffer = [];
            SKFont? skFont = null;

            for (int i = 0; i < glyphs.Count; i++)
            {
                ShapedGlyph glyph = glyphs[i];
                SKTypeface typeface = glyph.CharacterInfo.Typeface;
                float scale = glyph.Scale;

                if (skFont?.Typeface != typeface || Math.Abs(scale - skFont.ScaleX) / skFont.ScaleX >= scaleTolerancePercent)
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
            SKTypeface baseTypeface = glyphs[0].CharacterInfo.Typeface;
            using SKFont skFont = PdfPaintFactory.CreateTextFont(baseTypeface);
            DrawShapedText(processor, skFont, glyphs, state);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessType3(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfType3Font type3Font)
    {
        if (state.RenderingParameters.RenderText
            && state.TextRenderingMode != PdfTextRenderingMode.Invisible
            && state.TextRenderingMode != PdfTextRenderingMode.Clip)
        {
            // Type3 glyphs are recorded commands in glyph space (after FontMatrix). Apply text matrix and per-glyph offsets.
            processor.Process(SaveStateCommand.Instance);
            PdfMatrix fullTextMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);
            processor.Process(new ConcatMatrixCommand(fullTextMatrix));

            for (int i = 0; i < glyphs.Count; i++)
            {
                ShapedGlyph glyph = glyphs[i];
                PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode, _renderer, state);
                if (charInfo.IsDefined && charInfo.Recording != null)
                {
                    UncoloredPaintModifier? modifier = (charInfo.IsColored)
                        ? default
                        : new UncoloredPaintModifier(state.FillPaint.Color);

                    // Translate by glyph X/Y (already in text space units after fullTextMatrix).
                    PdfMatrix glyphMatrix = PdfMatrix.CreateTranslation(glyph.X, glyph.Y).PreConcat(type3Font.FontMatrix);
                    processor.Process(new DrawRecordingCommand(charInfo.Recording, glyphMatrix, modifier, disposeRecording: false));
                }
            }

            processor.Process(RestoreStateCommand.Instance);
        }

        if (state.RenderingParameters.ExtractText)
        {
            var characters = new PdfCharacter[glyphs.Count];

            for (int i = 0; i < glyphs.Count; i++)
            {
                ShapedGlyph glyph = glyphs[i];
                PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode, _renderer, state);
                PdfRectangle? glyphBBox = charInfo.BBox ?? type3Font.FontBBox;
                SKRect bounds;

                if (glyphBBox.HasValue)
                {
                    PdfRectangle mapped = type3Font.FontMatrix.MapRect(glyphBBox.Value);
                    bounds = new SKRect(glyph.X + mapped.Left, glyph.Y + mapped.Top, glyph.X + mapped.Right, glyph.Y + mapped.Bottom);
                }
                else
                {
                    bounds = new SKRect(glyph.X, glyph.Y, glyph.X + glyph.Advance, glyph.Y);
                }

                characters[i] = new PdfCharacter(glyph.CharacterInfo.Unicode, bounds);
            }

            PdfMatrix extractMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);
            EmitTextCharacters(processor, state, extractMatrix, characters);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawShapedText(IPdfCommandProcessor processor, SKFont font, List<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        if (state.RenderingParameters.RenderText)
        {
            if (ShouldFill(state.TextRenderingMode))
            {
                using TextFillRenderTarget textFillTarget = new(font, shapingResult, state);
                textFillTarget.Render(processor);
            }

            if (ShouldStroke(state.TextRenderingMode))
            {
                using TextStrokeRenderTarget textStrokeTarget = new(font, shapingResult, state);
                textStrokeTarget.Render(processor);
            }

            // Apply clipping if requested (modes with Clip). Pure clip mode skips drawing above.
            if (ShouldClip(state.TextRenderingMode))
            {
                using SKPath textPath = TextRenderUtilities.GetTextPath(shapingResult, font, state);
                if (!textPath.IsEmpty)
                {
                    state.TextClipPath ??= new PdfPath();
                    state.TextClipPath.AddPath(textPath.ToPdfPath());
                }
            }
        }

        if (state.RenderingParameters.ExtractText)
        {
            SKFontMetrics metrics = font.Metrics;
            var characters = new PdfCharacter[shapingResult.Count];

            for (int i = 0; i < shapingResult.Count; i++)
            {
                ShapedGlyph glyph = shapingResult[i];
                characters[i] = new PdfCharacter(
                    glyph.CharacterInfo.Unicode,
                    new SKRect(glyph.X, glyph.Y + metrics.Ascent, glyph.X + glyph.CharacterInfo.OriginalWidth, glyph.Y + metrics.Descent));
            }

            PdfMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(state);
            EmitTextCharacters(processor, state, textMatrix, characters);
        }
    }

    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    private static void EmitTextCharacters(IPdfCommandProcessor processor, PdfGraphicsState state, in PdfMatrix matrix, PdfCharacter[] characters)
    {
        PdfTextMarkup? pendingMarkup = state.PendingTextMarkup;
        if (pendingMarkup != null)
        {
            PdfMarkedContent markedContent = new(PdfString.Empty) { TextMarkup = pendingMarkup };
            processor.Process(new BeginMarkedContentCommand(markedContent));
            state.PendingTextMarkup = null;
        }

        processor.Process(new TextCharactersCommand(matrix, characters));

        if (pendingMarkup != null)
        {
            processor.Process(new EndMarkedContentCommand());
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
