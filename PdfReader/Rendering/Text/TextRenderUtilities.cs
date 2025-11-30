using PdfReader.Fonts.Model;
using PdfReader.Rendering.State;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Text;

/// <summary>
/// Utilities for text rendering operations.
/// </summary>
public class TextRenderUtilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPath GetTextPath(List<ShapedGlyph> shapingResult, SKFont font, PdfGraphicsState state)
    {
        var textPath = new SKPath();

        for (int i = 0; i < shapingResult.Count; i++)
        {
            var glyphId = shapingResult[i].GlyphId;
            if (glyphId != 0)
            {
                using var glyphPath = font.GetGlyphPath((ushort)glyphId);
                if (glyphPath != null)
                {
                    // Translate glyph outline by current advance
                    textPath.AddPath(glyphPath, SKMatrix.CreateTranslation(shapingResult[i].X, shapingResult[i].Y));
                }
            }
        }

        var matrix = GetFullTextMatrix(state);
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

        return shapingResult[shapingResult.Count - 1].X + shapingResult[shapingResult.Count - 1].Width;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTextHeight(List<ShapedGlyph> shapingResult)
    {
        if (shapingResult.Count == 0)
        {
            return 0;
        }

        return shapingResult[shapingResult.Count - 1].Y + shapingResult[shapingResult.Count - 1].Width;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKMatrix GetFullTextMatrix(PdfGraphicsState state)
    {
        var textMatrix = state.TextMatrix;

        if (state.Rise != 0)
        {
            textMatrix = SKMatrix.Concat(textMatrix, SKMatrix.CreateTranslation(0, state.Rise));
        }

        // Apply font size, horizontal scaling, and vertical flip
        var fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
        var fontScalingMatrix = SKMatrix.CreateScale(fullHorizontalScale, -state.FontSize);
        textMatrix = SKMatrix.Concat(textMatrix, fontScalingMatrix);

        return textMatrix;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKTextBlob BuildTextBlob(List<ShapedGlyph> shapingResult, SKFont font)
    {
        // Pre-count drawable glyphs (gid != 0) while computing positions using full advance including skipped glyphs.
        int drawableCount = 0;
        for (int i = 0; i < shapingResult.Count; i++)
        {
            if (shapingResult[i].GlyphId != 0)
            {
                drawableCount++;
            }
        }

        using var builder = new SKTextBlobBuilder();
        var run = builder.AllocatePositionedRun(font, drawableCount);
        var glyphSpan = run.Glyphs;
        var positionSpan = run.Positions;

        int drawIndex = 0;
        for (int index = 0; index < shapingResult.Count; index++)
        {
            var shapedGlyph = shapingResult[index];
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
