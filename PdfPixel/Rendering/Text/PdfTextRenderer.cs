using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Text;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Manages text drawing with proper selection and positioning.
/// </summary>
internal class PdfTextRenderer
{
    private readonly PdfRenderer _renderer;
    private readonly ILogger<PdfTextRenderer> _logger;

    public PdfTextRenderer(PdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _logger = loggerFactory.CreateLogger<PdfTextRenderer>();
    }

    /// <inheritdoc/>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfSize DrawTextSequence(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFont font)
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

        if (!state.RenderingParameters.ProcessText
            || (!state.RenderingParameters.RenderContent && !state.RenderingParameters.ExtractContent))
        {
            return PdfSize.Empty;
        }

        ProcessGlyphs(processor, glyphs, state, font);

        if (state.CurrentFont != null && state.CurrentFont.WritingMode == Fonts.Mapping.CMapWritingMode.Vertical)
        {
            return new PdfSize(0, -PdfTextRenderUtilities.GetTextHeight(glyphs) * state.FontSize);
        }
        else
        {
            float fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
            return new PdfSize(PdfTextRenderUtilities.GetTextWidth(glyphs) * fullHorizontalScale, 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessGlyphs(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFont font)
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
    private void ProcessType3(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> glyphsSpan, PdfGraphicsState state, PdfType3Font type3Font)
    {
        var glyphCommands = new DrawRecordingCommand?[glyphsSpan.Length];

        if (state.RenderingParameters.RenderContent
            && state.TextRenderingMode != PdfTextRenderingMode.Invisible
            && state.TextRenderingMode != PdfTextRenderingMode.Clip)
        {
            PdfMatrix fullTextMatrix = PdfTextRenderUtilities.GetFullTextMatrix(state, inverse: false);

            // Type3 glyphs are recorded commands in glyph space (after FontMatrix). Apply text matrix and per-glyph offsets.
            processor.Process(SaveStateCommand.Instance);
            processor.Process(new ConcatMatrixCommand(fullTextMatrix));

            for (int i = 0; i < glyphsSpan.Length; i++)
            {
                ShapedGlyph glyph = glyphsSpan[i];
                PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode);
                PdfCommandRecorder? recording = type3Font.RecordCharacter(charInfo, _renderer, state);

                if (recording != null)
                {
                    PdfPaint? tintPaint = (charInfo.IsColored)
                        ? null
                        : state.FillPaint;

                    // Translate by glyph X/Y (already in text space units after fullTextMatrix).
                    PdfMatrix glyphMatrix = PdfMatrix.CreateTranslation(glyph.X, glyph.Y).PreConcat(type3Font.FontMatrix);
                    DrawRecordingCommand glyphCommand = new(recording, glyphMatrix, tintPaint);
                    glyphCommands[i] = glyphCommand;
                    processor.Process(glyphCommand);
                }
            }

            processor.Process(RestoreStateCommand.Instance);
        }

        if (state.RenderingParameters.ExtractContent)
        {
            var characters = new PdfCharacter[glyphsSpan.Length];

            for (int i = 0; i < glyphsSpan.Length; i++)
            {
                ShapedGlyph glyph = glyphsSpan[i];
                PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode);
                characters[i] = new PdfCharacter(glyph.CharacterInfo.Unicode, GetType3CharacterBounds(glyph, charInfo, type3Font, glyphCommands[i]));
            }

            PdfMatrix extractMatrix = PdfTextRenderUtilities.GetFullTextMatrix(state, inverse: false);
            EmitTextCharacters(processor, extractMatrix, characters);
        }
    }

    /// <summary>
    /// Returns the box of a Type 3 character in text space: the box its glyph declares through d1 or the font's box,
    /// otherwise the area its drawn glyph paints, otherwise its advance along the baseline.
    /// </summary>
    private static PdfRectangle GetType3CharacterBounds(in ShapedGlyph glyph, PdfType3CharacterInfo charInfo, PdfType3Font type3Font, DrawRecordingCommand? glyphCommand)
    {
        PdfRectangle? glyphBoundingBox = charInfo.BoundingBox ?? type3Font.FontBoundingBox;
        PdfRectangle? recordedBounds = null;

        if (glyphBoundingBox == null && glyphCommand != null)
        {
            recordedBounds = glyphCommand.Recorder.GetBounds();
        }

        if (glyphBoundingBox != null)
        {
            PdfRectangle mapped = type3Font.FontMatrix.MapRect(glyphBoundingBox.Value);
            return new PdfRectangle(glyph.X + mapped.Left, glyph.Y + mapped.Top, glyph.X + mapped.Right, glyph.Y + mapped.Bottom);
        }
        else if (recordedBounds != null && glyphCommand != null)
        {
            return glyphCommand.Matrix.MapRect(recordedBounds.Value);
        }
        else
        {
            return new PdfRectangle(glyph.X, glyph.Y, glyph.X + glyph.Advance, glyph.Y);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawShapedText(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        if (state.RenderingParameters.RenderContent)
        {
            switch (state.TextRenderingMode)
            {
                case PdfTextRenderingMode.Fill:
                case PdfTextRenderingMode.FillAndClip:
                {
                    DrawTextFill(processor, shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.Stroke:
                case PdfTextRenderingMode.StrokeAndClip:
                {
                    DrawTextStroke(processor, shapingResult, state);
                    break;
                }
                case PdfTextRenderingMode.FillAndStroke:
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                {
                    DrawTextFillAndStroke(processor, shapingResult, state);
                    break;
                }
            }
        }

        switch (state.TextRenderingMode)
        {
            case PdfTextRenderingMode.FillAndClip:
            case PdfTextRenderingMode.StrokeAndClip:
            case PdfTextRenderingMode.FillAndStrokeAndClip:
            case PdfTextRenderingMode.Clip:
            {
                AddTextClip(processor, shapingResult, state);
                break;
            }
        }

        if (state.RenderingParameters.ExtractContent)
        {
            var characters = new PdfCharacter[CountCharacters(shapingResult)];
            int characterIndex = 0;

            for (int i = 0; i < shapingResult.Length; i++)
            {
                ShapedGlyph glyph = shapingResult[i];
                if (!IsCharacterStart(glyph))
                {
                    continue;
                }

                PdfFontMetrics metrics = glyph.CharacterInfo.Typeface.Metrics;

                // PdfFontMetrics uses the standard font convention: ascent up positive, descent down negative.
                characters[characterIndex] = new PdfCharacter(
                    glyph.CharacterInfo.Unicode,
                    new PdfRectangle(glyph.X, glyph.Y - metrics.Ascent, glyph.X + glyph.CharacterInfo.OriginalWidth, glyph.Y - metrics.Descent));

                characterIndex++;
            }

            PdfMatrix textMatrix = PdfTextRenderUtilities.GetFullTextMatrix(state);
            EmitTextCharacters(processor, textMatrix, characters);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextFill(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextFillRenderTarget textFillTarget = new(shapingResult.ToArray(), state);
        textFillTarget.Render(processor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextStroke(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextStrokeRenderTarget textStrokeTarget = new(shapingResult.ToArray(), state);
        textStrokeTarget.Render(processor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextFillAndStroke(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        ShapedGlyph[] glyphs = shapingResult.ToArray();
        TextFillRenderTarget textFillTarget = new(glyphs, state);
        TextStrokeRenderTarget textStrokeTarget = new(glyphs, state);

        PdfCommandRecorder recorder = ImplicitKnockoutGroup.Begin(state);
        textFillTarget.Render(recorder);
        textStrokeTarget.Render(recorder);
        ImplicitKnockoutGroup.Draw(processor, recorder, state);
    }

    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    private static void EmitTextCharacters(IPdfCommandProcessor processor, in PdfMatrix matrix, PdfCharacter[] characters)
    {
        if (characters.Length == 0)
        {
            return;
        }

        PdfRectangle bounds = characters[0].BoundingBox;
        for (int i = 1; i < characters.Length; i++)
        {
            bounds = PdfRectangle.Union(bounds, characters[i].BoundingBox);
        }

        processor.Process(new PdfContentCommand<PdfCharacter[]>(PdfContentKind.Text, matrix, bounds, characters));
    }

    /// <summary>
    /// Adds the glyphs' outlines to the clip the current text object sets at its end (ISO 32000-2 9.3.6).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTextClip(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        PdfMatrix textMatrix = PdfTextRenderUtilities.GetFullTextMatrix(state);

        processor.Process(new DrawShapedTextCommand(textMatrix, shapingResult.ToArray(), new PdfPaint(PdfPaintStyle.Clip)));
    }

    private static bool IsCharacterStart(in ShapedGlyph glyph) => glyph.GroupId == null || glyph.GroupId.Value == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountCharacters(in ReadOnlySpan<ShapedGlyph> glyphs)
    {
        int count = 0;

        for (int i = 0; i < glyphs.Length; i++)
        {
            if (IsCharacterStart(glyphs[i]))
            {
                count++;
            }
        }

        return count;
    }
}
