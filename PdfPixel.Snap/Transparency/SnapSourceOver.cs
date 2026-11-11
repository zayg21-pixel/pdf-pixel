using PdfPixel.Color.ColorSpace;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// Source-over for Normal blending in a non-knockout group, the exact reduction of <see cref="SnapGroupCompositing"/>
/// to P′ = (1 − αs)·P + αs·Cs, each pixel composited as one vector.
/// </summary>
internal static class SnapSourceOver
{
    private const int RgbComponentCount = 3;
    private const int CmykComponentCount = 4;
    private const int CmykPixelStride = 5;
    private const int CmykAlphaOffset = 4;

    /// <summary>
    /// Composites an element over the group's result and adds its shape to the group's, fg′ = fg ∪ fs.
    /// </summary>
    public static void ComposeElement(in SnapSourceRow row, in Span<float> results, in Span<float> groupShapes, PdfColorSpaceConverter colorSpace)
    {
        switch (colorSpace.GetBlendingColorModel())
        {
            case SnapBlendingColorModel.Gray:
            {
                ComposeElementGray(row, results, groupShapes);
                break;
            }
            case SnapBlendingColorModel.Rgb:
            {
                ComposeElementRgb(row, results, groupShapes);
                break;
            }
            default:
            {
                ComposeElementCmyk(row, results, groupShapes);
                break;
            }
        }
    }

    /// <summary>
    /// Composites a closed group in the target's color space straight from its premultiplied result, with f the
    /// row's coverage × mask × constant alpha: P′ = (1 − f·αg)·P + f·Pg, fg′ = fg ∪ fs, and αg′ = αg ∪ f·αg when
    /// <paramref name="groupAlphas"/> is not empty.
    /// </summary>
    public static void ComposeGroup(
        in SnapSourceRow row,
        in ReadOnlySpan<float> groupResults,
        in Span<float> results,
        in Span<float> groupShapes,
        in Span<float> groupAlphas,
        PdfColorSpaceConverter colorSpace)
    {
        switch (colorSpace.GetBlendingColorModel())
        {
            case SnapBlendingColorModel.Gray:
            {
                ComposeGroupGray(row, groupResults, results, groupShapes, groupAlphas);
                break;
            }
            case SnapBlendingColorModel.Rgb:
            {
                ComposeGroupRgb(row, groupResults, results, groupShapes, groupAlphas);
                break;
            }
            default:
            {
                ComposeGroupCmyk(row, groupResults, results, groupShapes, groupAlphas);
                break;
            }
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ComposeGroupGray(in SnapSourceRow row, in ReadOnlySpan<float> groupResults, in Span<float> results, in Span<float> groupShapes, in Span<float> groupAlphas)
    {
        ref Vector2 pixel = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<float, Vector2>(results));
        ref Vector2 source = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<float, Vector2>(groupResults));
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        bool keepsShape = !groupShapes.IsEmpty;
        int groupShapeStep = keepsShape ? 1 : 0;
        ref float groupAlpha = ref MemoryMarshal.GetReference(groupAlphas);
        bool keepsGroupAlphas = !groupAlphas.IsEmpty;
        int groupAlphaStep = keepsGroupAlphas ? 1 : 0;
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            if (coverage > 0)
            {
                Vector2 sourceValue = source * row.GetSourceAlpha(coverage, alpha, mask);
                float sourceAlpha = sourceValue.Y;

                if (keepsShape)
                {
                    groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));
                }

                if (keepsGroupAlphas)
                {
                    groupAlpha = Union(groupAlpha, sourceAlpha);
                }

                pixel = ComposePremultiplied(pixel, sourceValue, sourceAlpha);
            }

            pixel = ref Unsafe.Add(ref pixel, 1);
            source = ref Unsafe.Add(ref source, 1);
            groupShape = ref Unsafe.Add(ref groupShape, groupShapeStep);
            groupAlpha = ref Unsafe.Add(ref groupAlpha, groupAlphaStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ComposeGroupRgb(in SnapSourceRow row, in ReadOnlySpan<float> groupResults, in Span<float> results, in Span<float> groupShapes, in Span<float> groupAlphas)
    {
        ref Vector4 pixel = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<float, Vector4>(results));
        ref Vector4 source = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<float, Vector4>(groupResults));
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        bool keepsShape = !groupShapes.IsEmpty;
        int groupShapeStep = keepsShape ? 1 : 0;
        ref float groupAlpha = ref MemoryMarshal.GetReference(groupAlphas);
        bool keepsGroupAlphas = !groupAlphas.IsEmpty;
        int groupAlphaStep = keepsGroupAlphas ? 1 : 0;
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            if (coverage > 0)
            {
                Vector4 sourceValue = source * row.GetSourceAlpha(coverage, alpha, mask);
                float sourceAlpha = sourceValue.W;

                if (keepsShape)
                {
                    groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));
                }

                if (keepsGroupAlphas)
                {
                    groupAlpha = Union(groupAlpha, sourceAlpha);
                }

                pixel = ComposePremultiplied(pixel, sourceValue, sourceAlpha);
            }

            pixel = ref Unsafe.Add(ref pixel, 1);
            source = ref Unsafe.Add(ref source, 1);
            groupShape = ref Unsafe.Add(ref groupShape, groupShapeStep);
            groupAlpha = ref Unsafe.Add(ref groupAlpha, groupAlphaStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ComposeGroupCmyk(in SnapSourceRow row, in ReadOnlySpan<float> groupResults, in Span<float> results, in Span<float> groupShapes, in Span<float> groupAlphas)
    {
        ref float pixel = ref MemoryMarshal.GetReference(results);
        ref float source = ref MemoryMarshal.GetReference(groupResults);
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        bool keepsShape = !groupShapes.IsEmpty;
        int groupShapeStep = keepsShape ? 1 : 0;
        ref float groupAlpha = ref MemoryMarshal.GetReference(groupAlphas);
        bool keepsGroupAlphas = !groupAlphas.IsEmpty;
        int groupAlphaStep = keepsGroupAlphas ? 1 : 0;
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            if (coverage > 0)
            {
                float factor = row.GetSourceAlpha(coverage, alpha, mask);
                float sourceAlpha = Unsafe.Add(ref source, CmykAlphaOffset) * factor;
                ref Vector4 pixelColor = ref Unsafe.As<float, Vector4>(ref pixel);
                ref float pixelAlpha = ref Unsafe.Add(ref pixel, CmykAlphaOffset);

                if (keepsShape)
                {
                    groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));
                }

                if (keepsGroupAlphas)
                {
                    groupAlpha = Union(groupAlpha, sourceAlpha);
                }

                pixelColor = ComposePremultiplied(pixelColor, Unsafe.As<float, Vector4>(ref source) * factor, sourceAlpha);
                pixelAlpha = (pixelAlpha * (1 - sourceAlpha)) + sourceAlpha;
            }

            pixel = ref Unsafe.Add(ref pixel, CmykPixelStride);
            source = ref Unsafe.Add(ref source, CmykPixelStride);
            groupShape = ref Unsafe.Add(ref groupShape, groupShapeStep);
            groupAlpha = ref Unsafe.Add(ref groupAlpha, groupAlphaStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ComposeElementGray(in SnapSourceRow row, in Span<float> results, in Span<float> groupShapes)
    {
        ref Vector2 pixel = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<float, Vector2>(results));
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        bool keepsShape = !groupShapes.IsEmpty;
        int groupShapeStep = keepsShape ? 1 : 0;
        ref float color = ref MemoryMarshal.GetReference(row.Color.Values);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int colorStep = row.Color.Step;
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            if (coverage > 0)
            {
                float sourceAlpha = row.GetSourceAlpha(coverage, alpha, mask);

                if (keepsShape)
                {
                    groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));
                }

                pixel = ComposeOpaque(pixel, new Vector2(color, 1f), sourceAlpha);
            }

            pixel = ref Unsafe.Add(ref pixel, 1);
            groupShape = ref Unsafe.Add(ref groupShape, groupShapeStep);
            color = ref Unsafe.Add(ref color, colorStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ComposeElementRgb(in SnapSourceRow row, in Span<float> results, in Span<float> groupShapes)
    {
        ref Vector4 pixel = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<float, Vector4>(results));
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        bool keepsShape = !groupShapes.IsEmpty;
        int groupShapeStep = keepsShape ? 1 : 0;
        ref float color = ref MemoryMarshal.GetReference(row.Color.Values);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int colorStep = row.Color.Step * RgbComponentCount;
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            if (coverage > 0)
            {
                float sourceAlpha = row.GetSourceAlpha(coverage, alpha, mask);
                Vector4 source = new(color, Unsafe.Add(ref color, 1), Unsafe.Add(ref color, 2), 1f);

                if (keepsShape)
                {
                    groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));
                }

                pixel = ComposeOpaque(pixel, source, sourceAlpha);
            }

            pixel = ref Unsafe.Add(ref pixel, 1);
            groupShape = ref Unsafe.Add(ref groupShape, groupShapeStep);
            color = ref Unsafe.Add(ref color, colorStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ComposeElementCmyk(in SnapSourceRow row, in Span<float> results, in Span<float> groupShapes)
    {
        ref float pixel = ref MemoryMarshal.GetReference(results);
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        bool keepsShape = !groupShapes.IsEmpty;
        int groupShapeStep = keepsShape ? 1 : 0;
        ref float color = ref MemoryMarshal.GetReference(row.Color.Values);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int colorStep = row.Color.Step * CmykComponentCount;
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            if (coverage > 0)
            {
                float sourceAlpha = row.GetSourceAlpha(coverage, alpha, mask);
                ref Vector4 pixelColor = ref Unsafe.As<float, Vector4>(ref pixel);
                ref float pixelAlpha = ref Unsafe.Add(ref pixel, CmykAlphaOffset);

                if (keepsShape)
                {
                    groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));
                }

                pixelColor = ComposeOpaque(pixelColor, Unsafe.As<float, Vector4>(ref color), sourceAlpha);
                pixelAlpha = (pixelAlpha * (1 - sourceAlpha)) + sourceAlpha;
            }

            pixel = ref Unsafe.Add(ref pixel, CmykPixelStride);
            groupShape = ref Unsafe.Add(ref groupShape, groupShapeStep);
            color = ref Unsafe.Add(ref color, colorStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

    /// <summary>
    /// Returns P′ = (1 − αs)·P + Cs for a premultiplied source.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ComposePremultiplied(Vector2 pixel, Vector2 sourceValue, float sourceAlpha)
    {
        if (sourceAlpha >= 1)
        {
            return sourceValue;
        }
        else if (sourceAlpha <= 0)
        {
            return pixel;
        }
        else
        {
#if NET9_0_OR_GREATER
            return Vector2.FusedMultiplyAdd(pixel, new Vector2(1 - sourceAlpha), sourceValue);
#else
            return (pixel * (1 - sourceAlpha)) + sourceValue;
#endif
        }
    }

    /// <summary>
    /// Returns P′ = (1 − αs)·P + Cs for a premultiplied source.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 ComposePremultiplied(Vector4 pixel, Vector4 sourceValue, float sourceAlpha)
    {
        if (sourceAlpha >= 1)
        {
            return sourceValue;
        }
        else if (sourceAlpha <= 0)
        {
            return pixel;
        }
        else
        {
#if NET9_0_OR_GREATER
            return Vector4.FusedMultiplyAdd(pixel, new Vector4(1 - sourceAlpha), sourceValue);
#else
            return (pixel * (1 - sourceAlpha)) + sourceValue;
#endif
        }
    }

    /// <summary>
    /// Returns P′ = (1 − αs)·P + αs·Cs for a source of opaque color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ComposeOpaque(Vector2 pixel, Vector2 source, float sourceAlpha)
    {
        if (sourceAlpha >= 1)
        {
            return source;
        }
        else if (sourceAlpha <= 0)
        {
            return pixel;
        }
        else
        {
            return Vector2.Lerp(pixel, source, sourceAlpha);
        }
    }

    /// <summary>
    /// Returns P′ = (1 − αs)·P + αs·Cs for a source of opaque color.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 ComposeOpaque(Vector4 pixel, Vector4 source, float sourceAlpha)
    {
        if (sourceAlpha >= 1)
        {
            return source;
        }
        else if (sourceAlpha <= 0)
        {
            return pixel;
        }
        else
        {
            return Vector4.Lerp(pixel, source, sourceAlpha);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Union(float backdrop, float source) => backdrop + source - (backdrop * source);
}
