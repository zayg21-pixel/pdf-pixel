using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Rendering.Text;
using PdfPixel.TextExtraction;
using PdfPixel.Transparency.Utilities;
using System;
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
    public PdfSize DrawTextSequence(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase? font)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (font == null || glyphs.Length == 0)
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
    private void ProcessGlyphs(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        if (font is PdfType3Font type3Font)
        {
            ProcessType3(processor, glyphs, state, type3Font);
        }
        else if (glyphs.Length > 0)
        {
            // Per-glyph typeface and scale (relevant when the font is substituted) are resolved by
            // DrawShapedTextCommand itself, grouping into spans only when they actually differ.
            DrawShapedText(processor, glyphs, state);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessType3(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> glyphs, PdfGraphicsState state, PdfType3Font type3Font)
    {
        ReadOnlySpan<ShapedGlyph> glyphsSpan = glyphs.Span;

        if (state.RenderingParameters.RenderText
            && state.TextRenderingMode != PdfTextRenderingMode.Invisible
            && state.TextRenderingMode != PdfTextRenderingMode.Clip)
        {
            // Type3 glyphs are recorded commands in glyph space (after FontMatrix). Apply text matrix and per-glyph offsets.
            processor.Process(SaveStateCommand.Instance);
            PdfMatrix fullTextMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);
            processor.Process(new ConcatMatrixCommand(fullTextMatrix));

            for (int i = 0; i < glyphsSpan.Length; i++)
            {
                ShapedGlyph glyph = glyphsSpan[i];
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
            var characters = new PdfCharacter[glyphsSpan.Length];

            for (int i = 0; i < glyphsSpan.Length; i++)
            {
                ShapedGlyph glyph = glyphsSpan[i];
                PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode, _renderer, state);
                PdfRectangle? glyphBBox = charInfo.BBox ?? type3Font.FontBBox;
                PdfRectangle bounds;

                if (glyphBBox.HasValue)
                {
                    PdfRectangle mapped = type3Font.FontMatrix.MapRect(glyphBBox.Value);
                    bounds = new PdfRectangle(glyph.X + mapped.Left, glyph.Y + mapped.Top, glyph.X + mapped.Right, glyph.Y + mapped.Bottom);
                }
                else
                {
                    bounds = new PdfRectangle(glyph.X, glyph.Y, glyph.X + glyph.Advance, glyph.Y);
                }

                characters[i] = new PdfCharacter(glyph.CharacterInfo.Unicode, bounds);
            }

            PdfMatrix extractMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);
            EmitTextCharacters(processor, state, extractMatrix, characters);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawShapedText(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        if (state.RenderingParameters.RenderText)
        {
            if (ShouldFill(state.TextRenderingMode))
            {
                TextFillRenderTarget textFillTarget = new(shapingResult, state);
                textFillTarget.Render(processor);
            }

            if (ShouldStroke(state.TextRenderingMode))
            {
                TextStrokeRenderTarget textStrokeTarget = new(shapingResult, state);
                textStrokeTarget.Render(processor);
            }

            // Apply clipping if requested (modes with Clip). Pure clip mode skips drawing above.
            if (ShouldClip(state.TextRenderingMode))
            {
                PdfPath textPath = TextRenderUtilities.GetTextPath(shapingResult, state);
                if (!textPath.IsEmpty)
                {
                    state.TextClipPath ??= new PdfPathBuilder();
                    state.TextClipPath.AddPath(textPath);
                }
            }
        }

        if (state.RenderingParameters.ExtractText)
        {
            ReadOnlySpan<ShapedGlyph> glyphsSpan = shapingResult.Span;
            var characters = new PdfCharacter[glyphsSpan.Length];

            for (int i = 0; i < glyphsSpan.Length; i++)
            {
                ShapedGlyph glyph = glyphsSpan[i];
                PdfFontMetrics metrics = glyph.CharacterInfo.Typeface.Metrics;

                // PdfFontMetrics uses the standard font convention (ascent up positive, descent down
                // negative), so this is the sign-flipped mirror of the same rectangle built from
                // Skia's (ascent down positive, descent up negative) SKFontMetrics.
                characters[i] = new PdfCharacter(
                    glyph.CharacterInfo.Unicode,
                    new PdfRectangle(glyph.X, glyph.Y - metrics.Ascent, glyph.X + glyph.CharacterInfo.OriginalWidth, glyph.Y - metrics.Descent));
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
