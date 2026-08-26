using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Commands.Model;
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
    public PdfSize DrawTextSequence(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        if (glyphs.Length == 0)
        {
            return PdfSize.Empty;
        }

        if (!state.RenderingParameters.RenderText && !state.RenderingParameters.ExtractText)
        {
            return PdfSize.Empty;
        }

        ProcessGlyphs(processor, glyphs, state, font);

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
            PdfMatrix fullTextMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);

            PdfRectangle contentBounds = TextRenderUtilities.GetType3Bounds(_renderer, glyphsSpan, state, type3Font, fullTextMatrix) ?? state.GetUserSpaceClipBounds();

            using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state, contentBounds);
            softMaskScope.BeginDrawContent();

            // Type3 glyphs are recorded commands in glyph space (after FontMatrix). Apply text matrix and per-glyph offsets.
            processor.Process(SaveStateCommand.Instance);
            processor.Process(new ConcatMatrixCommand(fullTextMatrix));

            for (int i = 0; i < glyphsSpan.Length; i++)
            {
                ShapedGlyph glyph = glyphsSpan[i];
                PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode, _renderer, state);
                if (charInfo.IsDefined && charInfo.Recording != null)
                {
                    PdfColor? tintColor = (charInfo.IsColored)
                        ? null
                        : state.FillPaint.Color;

                    // Translate by glyph X/Y (already in text space units after fullTextMatrix).
                    PdfMatrix glyphMatrix = PdfMatrix.CreateTranslation(glyph.X, glyph.Y).PreConcat(type3Font.FontMatrix);
                    processor.Process(new DrawRecordingCommand(charInfo.Recording, glyphMatrix, tintColor));
                }
            }

            processor.Process(RestoreStateCommand.Instance);

            softMaskScope.EndDrawContent();
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
            switch (state.TextRenderingMode)
            {
                case PdfTextRenderingMode.Fill:
                {
                    DrawTextFill(processor, shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.Stroke:
                {
                    DrawTextStroke(processor, shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.FillAndStroke:
                {
                    DrawTextFillAndStroke(processor, shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.Invisible:
                {
                    break;
                }
                case PdfTextRenderingMode.FillAndClip:
                {
                    DrawTextFill(processor, shapingResult, state);
                    AppendTextClip(shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.StrokeAndClip:
                {
                    DrawTextStroke(processor, shapingResult, state);
                    AppendTextClip(shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                {
                    DrawTextFillAndStroke(processor, shapingResult, state);
                    AppendTextClip(shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.Clip:
                {
                    AppendTextClip(shapingResult, state);
                    break;
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

                // PdfFontMetrics uses the standard font convention: ascent up positive, descent down negative.
                characters[i] = new PdfCharacter(
                    glyph.CharacterInfo.Unicode,
                    new PdfRectangle(glyph.X, glyph.Y - metrics.Ascent, glyph.X + glyph.CharacterInfo.OriginalWidth, glyph.Y - metrics.Descent));
            }

            PdfMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(state);
            EmitTextCharacters(processor, state, textMatrix, characters);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextFill(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextFillRenderTarget textFillTarget = new(shapingResult, state);

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state, textFillTarget.Bounds);
        softMaskScope.BeginDrawContent();
        textFillTarget.Render(processor);
        softMaskScope.EndDrawContent();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextStroke(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextStrokeRenderTarget textStrokeTarget = new(shapingResult, state);

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state, textStrokeTarget.Bounds);
        softMaskScope.BeginDrawContent();
        textStrokeTarget.Render(processor);
        softMaskScope.EndDrawContent();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextFillAndStroke(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextStrokeRenderTarget textStrokeTarget = new(shapingResult, state);

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state, textStrokeTarget.Bounds);
        softMaskScope.BeginDrawContent();

        bool overlapAffectsCompositing = state.FillPaint.RequiresComposition
            || state.StrokePaint.RequiresComposition;

        if (overlapAffectsCompositing)
        {
            PdfPath textPath = TextRenderUtilities.GetTextPath(shapingResult, state);

            processor.Process(SaveStateCommand.Instance);
            processor.Process(new ClipPathCommand(textPath, PdfClipOperation.Difference, state.StrokePaint));

            TextFillRenderTarget clippedFillTarget = new(shapingResult, state);
            clippedFillTarget.Render(processor);

            processor.Process(RestoreStateCommand.Instance);
        }
        else
        {
            TextFillRenderTarget textFillTarget = new(shapingResult, state);
            textFillTarget.Render(processor);
        }

        textStrokeTarget.Render(processor);
        softMaskScope.EndDrawContent();
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
    private static void AppendTextClip(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        PdfPath textPath = TextRenderUtilities.GetTextPath(shapingResult, state);
        if (!textPath.IsEmpty)
        {
            state.TextClipPath ??= new PdfPathBuilder();
            state.TextClipPath.AddPath(textPath);
        }
    }
}
