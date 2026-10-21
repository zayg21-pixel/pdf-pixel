using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
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
        if (state.RenderingParameters.RenderContent
            && state.TextRenderingMode != PdfTextRenderingMode.Invisible
            && state.TextRenderingMode != PdfTextRenderingMode.Clip)
        {
            PdfMatrix fullTextMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);

            PdfRectangle contentBounds = TextRenderUtilities.GetType3Bounds(glyphsSpan, type3Font, fullTextMatrix) ?? state.GetUserSpaceClipBounds();

            using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, contentBounds);

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
                    PdfColor? tintColor = (charInfo.IsColored)
                        ? null
                        : state.FillPaint.Color;

                    // Translate by glyph X/Y (already in text space units after fullTextMatrix).
                    PdfMatrix glyphMatrix = PdfMatrix.CreateTranslation(glyph.X, glyph.Y).PreConcat(type3Font.FontMatrix);
                    processor.Process(new DrawRecordingCommand(recording, glyphMatrix, tintColor));
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
                PdfRectangle? glyphBoundingBox = charInfo.BoundingBox ?? type3Font.FontBoundingBox;
                PdfRectangle bounds;

                if (glyphBoundingBox.HasValue)
                {
                    PdfRectangle mapped = type3Font.FontMatrix.MapRect(glyphBoundingBox.Value);
                    bounds = new PdfRectangle(glyph.X + mapped.Left, glyph.Y + mapped.Top, glyph.X + mapped.Right, glyph.Y + mapped.Bottom);
                }
                else
                {
                    bounds = new PdfRectangle(glyph.X, glyph.Y, glyph.X + glyph.Advance, glyph.Y);
                }

                characters[i] = new PdfCharacter(glyph.CharacterInfo.Unicode, bounds);
            }

            PdfMatrix extractMatrix = TextRenderUtilities.GetFullTextMatrix(state, inverse: false);
            EmitTextCharacters(processor, extractMatrix, characters);
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
                AppendTextClip(shapingResult, state);
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

            PdfMatrix textMatrix = TextRenderUtilities.GetFullTextMatrix(state);
            EmitTextCharacters(processor, textMatrix, characters);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextFill(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextFillRenderTarget textFillTarget = new(shapingResult.ToArray(), state);

        using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, textFillTarget.Bounds);
        textFillTarget.Render(processor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextStroke(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextStrokeRenderTarget textStrokeTarget = new(shapingResult, state);

        using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, textStrokeTarget.Bounds);
        textStrokeTarget.Render(processor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawTextFillAndStroke(IPdfCommandProcessor processor, in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        TextStrokeRenderTarget textStrokeTarget = new(shapingResult, state);

        using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, textStrokeTarget.Bounds);

        bool overlapAffectsCompositing = state.FillPaint.RequiresComposition
            || state.StrokePaint.RequiresComposition;

        if (overlapAffectsCompositing)
        {
            PdfPath textPath = TextRenderUtilities.GetTextPath(shapingResult, state);

            processor.Process(SaveStateCommand.Instance);
            processor.Process(new ClipPathCommand(textPath, PdfClipOperation.Difference, state.StrokePaint));

            TextFillRenderTarget clippedFillTarget = new(shapingResult.ToArray(), state);
            clippedFillTarget.Render(processor);

            processor.Process(RestoreStateCommand.Instance);
        }
        else
        {
            TextFillRenderTarget textFillTarget = new(shapingResult.ToArray(), state);
            textFillTarget.Render(processor);
        }

        textStrokeTarget.Render(processor);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendTextClip(in ReadOnlySpan<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        PdfPath textPath = TextRenderUtilities.GetTextPath(shapingResult, state);
        if (!textPath.IsEmpty)
        {
            state.TextClipPath ??= new PdfPathBuilder();
            state.TextClipPath.AddPath(textPath);
        }
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
