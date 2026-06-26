using PdfPixel.Rendering.State;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Utilities for text rendering operations.
/// </summary>
internal static class TextRenderUtilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPath GetTextPath(List<ShapedGlyph> shapingResult, SKFont font, PdfGraphicsState state)
    {
        using SKPathBuilder textPathBuilder = new();

        for (int i = 0; i < shapingResult.Count; i++)
        {
            uint glyphId = shapingResult[i].GlyphId;
            if (glyphId != 0)
            {
                using SKPath glyphPath = font.GetGlyphPath((ushort)glyphId);
                if (glyphPath != null)
                {
                    textPathBuilder.AddPath(glyphPath, SKMatrix.CreateTranslation(shapingResult[i].X, shapingResult[i].Y));
                }
            }
        }

        SKPath textPath = textPathBuilder.Detach();

        SKMatrix matrix = GetFullTextMatrix(state);
        textPath.Transform(matrix);

        return textPath;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTextWidth(List<ShapedGlyph> shapingResult)
    {
        if (shapingResult.Count == 0)
        {
            return 0;
        }

        return shapingResult[shapingResult.Count - 1].X + shapingResult[shapingResult.Count - 1].Advance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTextHeight(List<ShapedGlyph> shapingResult)
    {
        if (shapingResult.Count == 0)
        {
            return 0;
        }

        return shapingResult[shapingResult.Count - 1].Y + shapingResult[shapingResult.Count - 1].Advance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKMatrix GetFullTextMatrix(PdfGraphicsState state, bool inverse = true)
    {
        SKMatrix textMatrix = state.TextMatrix;

        if (state.Rise != 0)
        {
            textMatrix = SKMatrix.Concat(textMatrix, SKMatrix.CreateTranslation(0, state.Rise));
        }

        // Apply font size, horizontal scaling, and vertical flip
        float fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
        int verticalFlip = inverse ? -1 : 1;
        SKMatrix fontScalingMatrix = SKMatrix.CreateScale(fullHorizontalScale, state.FontSize * verticalFlip);
        return SKMatrix.Concat(textMatrix, fontScalingMatrix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKTextBlob? BuildTextBlob(ShapedGlyph[] shapingResult, SKFont font)
    {
        // Pre-count drawable glyphs (gid != 0) while computing positions using full advance including skipped glyphs.
        int drawableCount = 0;
        for (int i = 0; i < shapingResult.Length; i++)
        {
            if (shapingResult[i].GlyphId != 0)
            {
                drawableCount++;
            }
        }

        using SKTextBlobBuilder builder = new();
        SKPositionedRunBuffer run = builder.AllocatePositionedRun(font, drawableCount);
        System.Span<ushort> glyphSpan = run.Glyphs;
        System.Span<SKPoint> positionSpan = run.Positions;

        int drawIndex = 0;
        for (int index = 0; index < shapingResult.Length; index++)
        {
            ShapedGlyph shapedGlyph = shapingResult[index];
            // Record position regardless to advance subsequent glyphs.
            if (shapedGlyph.GlyphId != 0)
            {
                glyphSpan[drawIndex] = (ushort)shapedGlyph.GlyphId;
                positionSpan[drawIndex] = new SKPoint(shapedGlyph.X, shapedGlyph.Y);
                drawIndex++;
            }
        }

        return builder.Build();
    }
}
