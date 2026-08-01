using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Utilities for text rendering operations.
/// </summary>
internal static class TextRenderUtilities
{
    /// <summary>
    /// Builds the combined glyph outline for <paramref name="shapingResult"/>, transformed into the same
    /// space as other drawing content. Each glyph's outline comes from its own
    /// <see cref="PdfCharacterInfo.Typeface"/> in raw, unscaled form, so it is transformed by its
    /// horizontal <see cref="ShapedGlyph.Scale"/> and position before being combined with the rest.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPath GetTextPath(in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfGraphicsState state)
    {
        PdfPathBuilder textPathBuilder = new();
        ReadOnlySpan<ShapedGlyph> glyphs = shapingResult.Span;

        for (int i = 0; i < glyphs.Length; i++)
        {
            ShapedGlyph shapedGlyph = glyphs[i];
            ushort? glyphId = shapedGlyph.GlyphId;
            if (glyphId == null)
            {
                continue;
            }

            IPdfTypeface typeface = shapedGlyph.CharacterInfo.Typeface;
            ReadOnlyMemory<byte> glyphPathData = typeface.GetPath(glyphId.Value);
            if (glyphPathData.IsEmpty)
            {
                continue;
            }

            PdfPath glyphPath = new(glyphPathData, PdfPathFillType.Winding);
            PdfMatrix glyphMatrix = PdfMatrix.CreateScaleTranslation(shapedGlyph.Scale, 1f, shapedGlyph.X, shapedGlyph.Y);
            textPathBuilder.AddPath(glyphPath.Transform(glyphMatrix));
        }

        PdfMatrix matrix = GetFullTextMatrix(state, inverse: false);
        return textPathBuilder.ToPath().Transform(matrix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTextWidth(in ReadOnlyMemory<ShapedGlyph> shapingResult)
    {
        if (shapingResult.IsEmpty)
        {
            return 0;
        }

        ShapedGlyph last = shapingResult.Span[shapingResult.Length - 1];
        return last.X - last.CharacterInfo.Offset.X + last.Advance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTextHeight(in ReadOnlyMemory<ShapedGlyph> shapingResult)
    {
        if (shapingResult.IsEmpty)
        {
            return 0;
        }

        ShapedGlyph last = shapingResult.Span[shapingResult.Length - 1];
        return last.Y - last.CharacterInfo.Offset.Y + last.Advance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfMatrix GetFullTextMatrix(PdfGraphicsState state, bool inverse = true)
    {
        PdfMatrix textMatrix = state.TextMatrix;

        if (state.Rise != 0)
        {
            textMatrix = PdfMatrix.Concat(textMatrix, PdfMatrix.CreateTranslation(0, state.Rise));
        }

        // Apply font size, horizontal scaling, and vertical flip
        float fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
        int verticalFlip = inverse ? -1 : 1;
        PdfMatrix fontScalingMatrix = PdfMatrix.CreateScale(fullHorizontalScale, state.FontSize * verticalFlip);
        return PdfMatrix.Concat(textMatrix, fontScalingMatrix);
    }
}
