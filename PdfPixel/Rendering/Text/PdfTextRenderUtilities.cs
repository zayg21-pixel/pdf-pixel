using PdfPixel.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Text;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Utilities for text rendering operations.
/// </summary>
public static class PdfTextRenderUtilities
{
    /// <summary>
    /// Builds the combined glyph outline for <paramref name="glyphs"/>, transformed into the same
    /// space as other drawing content. Each glyph's outline comes from its own
    /// <see cref="PdfCharacterInfo.Typeface"/> in raw, unscaled form, so it is transformed by its
    /// horizontal <see cref="ShapedGlyph.Scale"/> and position before being combined with the rest.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfPath GetTextPath(in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state) => GetTextPath(glyphs, GetFullTextMatrix(state));

    /// <summary>
    /// Builds the combined glyph outline for <paramref name="glyphs"/>, transformed by
    /// <paramref name="matrix"/>, which maps the text space the glyphs are positioned in.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPath GetTextPath(in ReadOnlySpan<ShapedGlyph> glyphs, in PdfMatrix matrix)
    {
        PdfPathBuilder textPathBuilder = new(matrix);

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
            PdfMatrix glyphMatrix = PdfMatrix.CreateScaleTranslation(shapedGlyph.Scale, -1f, shapedGlyph.X, shapedGlyph.Y);
            textPathBuilder.AddPath(glyphPath.Transform(glyphMatrix));
        }

        return textPathBuilder.Detach();
    }

    /// <summary>
    /// Computes the area the glyphs of <paramref name="glyphs"/> can cover, in the same space
    /// <see cref="GetTextPath(in ReadOnlySpan{ShapedGlyph}, PdfGraphicsState)"/> builds its outline in. Each glyph contributes the font bounding box its
    /// <see cref="PdfCharacterInfo.Typeface"/> reports - the box every glyph of that font fits inside -
    /// placed and scaled the way that glyph is, which bounds the run without reading a single outline.
    /// Returns <see cref="PdfRectangle.Empty"/> when no glyph is drawn, and <see langword="null"/> when a glyph's typeface
    /// reports no usable box, leaving the extent of the run unknown rather than under-stated.
    /// </summary>
    internal static PdfRectangle? GetTextBounds(in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state)
        => GetTextBounds(glyphs, GetFullTextMatrix(state));

    /// <summary>
    /// Computes the area the glyphs of <paramref name="glyphs"/> can cover in the space <paramref name="matrix"/>
    /// maps their text space into, the same space <see cref="GetTextPath(in ReadOnlySpan{ShapedGlyph}, in PdfMatrix)"/>
    /// builds its outline in. Returns <see cref="PdfRectangle.Empty"/> when no glyph is drawn, and
    /// <see langword="null"/> when a glyph's typeface reports no usable box.
    /// </summary>
    public static PdfRectangle? GetTextBounds(in ReadOnlySpan<ShapedGlyph> glyphs, in PdfMatrix matrix)
    {
        if (glyphs.Length == 0)
        {
            return PdfRectangle.Empty;
        }

        var hasBounds = false;
        float left = float.MaxValue;
        float top = float.MaxValue;
        float right = float.MinValue;
        float bottom = float.MinValue;

        for (int i = 0; i < glyphs.Length; i++)
        {
            ShapedGlyph shapedGlyph = glyphs[i];
            if (shapedGlyph.GlyphId == null)
            {
                continue;
            }

            PdfFontMetrics metrics = shapedGlyph.CharacterInfo.Typeface.Metrics;

            if (metrics.BoundingBoxRight <= metrics.BoundingBoxLeft || metrics.BoundingBoxTop <= metrics.BoundingBoxBottom)
            {
                // incorrect metrics, can't produce BBox
                return null;
            }

            float scaledLeft = shapedGlyph.X + (metrics.BoundingBoxLeft * shapedGlyph.Scale);
            float scaledRight = shapedGlyph.X + (metrics.BoundingBoxRight * shapedGlyph.Scale);

            hasBounds = true;
            left = Math.Min(left, Math.Min(scaledLeft, scaledRight));
            right = Math.Max(right, Math.Max(scaledLeft, scaledRight));
            top = Math.Min(top, shapedGlyph.Y - metrics.BoundingBoxTop);
            bottom = Math.Max(bottom, shapedGlyph.Y - metrics.BoundingBoxBottom);
        }

        if (!hasBounds)
        {
            return PdfRectangle.Empty;
        }

        return matrix.MapRect(new PdfRectangle(left, top, right, bottom));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetTextWidth(in ReadOnlySpan<ShapedGlyph> shapingResult)
    {
        if (shapingResult.IsEmpty)
        {
            return 0;
        }

        ShapedGlyph last = shapingResult[shapingResult.Length - 1];
        return last.X - last.CharacterInfo.Offset.X + last.Advance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetTextHeight(in ReadOnlySpan<ShapedGlyph> shapingResult)
    {
        if (shapingResult.IsEmpty)
        {
            return 0;
        }

        ShapedGlyph last = shapingResult[shapingResult.Length - 1];
        return last.Y - last.CharacterInfo.Offset.Y + last.Advance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfMatrix GetFullTextMatrix(PdfGraphicsState state, bool inverse = true)
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
