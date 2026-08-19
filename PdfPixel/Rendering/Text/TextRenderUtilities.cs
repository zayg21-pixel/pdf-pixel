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
        PdfMatrix matrix = GetFullTextMatrix(state, inverse: false);
        PdfPathBuilder textPathBuilder = new(matrix);
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

        return textPathBuilder.Detach();
    }

    /// <summary>
    /// Computes the area the glyphs of <paramref name="glyphs"/> can cover, in the same space
    /// <see cref="GetTextPath"/> builds its outline in. Each glyph contributes the font bounding box its
    /// <see cref="PdfCharacterInfo.Typeface"/> reports - the box every glyph of that font fits inside -
    /// placed and scaled the way that glyph is, which bounds the run without reading a single outline.
    /// Returns <see langword="null"/> when a glyph's typeface reports no usable box, leaving the extent
    /// of the run unknown rather than under-stated.
    /// </summary>
    public static PdfRectangle? GetTextBounds(in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state)
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
            top = Math.Min(top, shapedGlyph.Y + metrics.BoundingBoxBottom);
            bottom = Math.Max(bottom, shapedGlyph.Y + metrics.BoundingBoxTop);
        }

        if (!hasBounds)
        {
            return null;
        }

        PdfMatrix matrix = GetFullTextMatrix(state, inverse: false);
        return matrix.MapRect(new PdfRectangle(left, top, right, bottom));
    }


    /// <summary>
    /// Computes the area the Type 3 glyphs of <paramref name="glyphs"/> can cover, in the space
    /// <paramref name="fullTextMatrix"/> maps into. Each glyph contributes the box its CharProc declared
    /// through d1, or the font's own box when it declared none, and a glyph with neither yields
    /// <see langword="null"/>.
    /// </summary>
    public static PdfRectangle? GetType3Bounds(IPdfRenderer renderer, in ReadOnlySpan<ShapedGlyph> glyphs, PdfGraphicsState state, PdfType3Font type3Font, in PdfMatrix fullTextMatrix)
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
            ShapedGlyph glyph = glyphs[i];
            PdfType3CharacterInfo charInfo = type3Font.GetCharacterInfo(glyph.CharacterInfo.CharacterCode, renderer, state);

            if (!charInfo.IsDefined || charInfo.Recording == null)
            {
                continue;
            }

            PdfRectangle? glyphBBox = charInfo.BBox ?? type3Font.FontBBox;

            if (glyphBBox == null)
            {
                return null;
            }

            PdfRectangle mapped = type3Font.FontMatrix.MapRect(glyphBBox.Value);

            if (mapped.Width <= 0 || mapped.Height <= 0)
            {
                return null;
            }

            hasBounds = true;
            left = Math.Min(left, glyph.X + mapped.Left);
            top = Math.Min(top, glyph.Y + mapped.Top);
            right = Math.Max(right, glyph.X + mapped.Right);
            bottom = Math.Max(bottom, glyph.Y + mapped.Bottom);
        }

        if (!hasBounds)
        {
            return null;
        }

        return fullTextMatrix.MapRect(new PdfRectangle(left, top, right, bottom));
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
