using PdfPixel.Color.ColorSpace;
using PdfPixel.Snap.Painting;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// The group compositing formulas of ISO 32000-2 11.4.8 on premultiplied color (P = α·C), reduced to non-knockout,
/// isolated knockout and non-isolated knockout groups. A result holds per pixel the premultiplied components followed
/// by alpha.
/// </summary>
internal static class SnapGroupCompositing
{
    /// <summary>
    /// Composites an element in a non-knockout group: P′ = (1 − αs)·P + αs·S and α′ = (1 − αs)·α + αs.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void ComposeOverResult(
        in SnapSourceRow row,
        in SnapRowValues sourceTerms,
        in Span<float> results,
        int componentCount)
    {
        int pixelStride = componentCount + 1;
        int termStep = sourceTerms.Step * componentCount;
        ref float result = ref MemoryMarshal.GetReference(results);
        ref float term = ref MemoryMarshal.GetReference(sourceTerms.Values);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int coverageStep = row.Coverage.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            float sourceAlpha = row.GetSourceAlpha(coverage, alpha, mask);

            if (sourceAlpha > 0)
            {
                float keep = 1 - sourceAlpha;

                for (int component = 0; component < componentCount; component++)
                {
                    ref float value = ref Unsafe.Add(ref result, component);
                    value = (keep * value) + (sourceAlpha * Unsafe.Add(ref term, component));
                }

                ref float resultAlpha = ref Unsafe.Add(ref result, componentCount);
                resultAlpha = (keep * resultAlpha) + sourceAlpha;
            }

            result = ref Unsafe.Add(ref result, pixelStride);
            term = ref Unsafe.Add(ref term, termStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

    /// <summary>
    /// Splits a closed group's result into an element's color and alpha: Cs = Pg / αg and αg.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void SplitGroupResult(
        in ReadOnlySpan<float> groupValues,
        in Span<float> sourceColors,
        in Span<float> sourceAlphas,
        int componentCount)
    {
        int pixelStride = componentCount + 1;
        ref float groupValue = ref MemoryMarshal.GetReference(groupValues);
        ref float sourceColor = ref MemoryMarshal.GetReference(sourceColors);
        ref float sourceAlpha = ref MemoryMarshal.GetReference(sourceAlphas);

        for (int index = 0; index < sourceAlphas.Length; index++)
        {
            float groupAlpha = Unsafe.Add(ref groupValue, componentCount);
            float inverseAlpha = (groupAlpha > 0) ? 1 / groupAlpha : 0;

            for (int component = 0; component < componentCount; component++)
            {
                Unsafe.Add(ref sourceColor, component) = Unsafe.Add(ref groupValue, component) * inverseAlpha;
            }

            sourceAlpha = groupAlpha;

            groupValue = ref Unsafe.Add(ref groupValue, pixelStride);
            sourceColor = ref Unsafe.Add(ref sourceColor, componentCount);
            sourceAlpha = ref Unsafe.Add(ref sourceAlpha, 1);
        }
    }

    /// <summary>
    /// Adds the element's alpha to the group alpha in a non-knockout group: αg′ = αg ∪ αs.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void AccumulateGroupAlphas(in SnapSourceRow row, in Span<float> groupAlphas)
    {
        ref float groupAlpha = ref MemoryMarshal.GetReference(groupAlphas);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float alpha = ref MemoryMarshal.GetReference(row.Alpha.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int coverageStep = row.Coverage.Step;
        int alphaStep = row.Alpha.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            groupAlpha = Union(groupAlpha, row.GetSourceAlpha(coverage, alpha, mask));

            groupAlpha = ref Unsafe.Add(ref groupAlpha, 1);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

    /// <summary>
    /// Composites an element in an isolated knockout group, where the blend mode has no effect:
    /// P′ = (1 − fs)·P + αs·Cs and α′ = (1 − fs)·α + αs.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void ComposeKnockoutIsolated(in SnapSourceRow row, in Span<float> results, int componentCount)
    {
        int pixelStride = componentCount + 1;
        int colorStep = row.Color.Step * componentCount;
        ref float result = ref MemoryMarshal.GetReference(results);
        ref float color = ref MemoryMarshal.GetReference(row.Color.Values);
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
            float sourceShape = row.GetSourceShape(coverage, shape, mask);

            if (sourceShape > 0)
            {
                float sourceAlpha = row.GetSourceAlpha(coverage, alpha, mask);
                float keep = 1 - sourceShape;

                for (int component = 0; component < componentCount; component++)
                {
                    ref float value = ref Unsafe.Add(ref result, component);
                    value = (keep * value) + (sourceAlpha * Unsafe.Add(ref color, component));
                }

                ref float resultAlpha = ref Unsafe.Add(ref result, componentCount);
                resultAlpha = (keep * resultAlpha) + sourceAlpha;
            }

            result = ref Unsafe.Add(ref result, pixelStride);
            color = ref Unsafe.Add(ref color, colorStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

    /// <summary>
    /// Composites an element in a non-isolated knockout group: P′ = (1 − fs)·P + (fs − αs)·P₀ + αs·S,
    /// α′ = (1 − fs)·α + (fs − αs)·α₀ + αs and αg′ = (1 − fs)·αg + αs.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void ComposeKnockout(
        in SnapSourceRow row,
        in SnapRowValues sourceTerms,
        in ReadOnlySpan<float> backdrops,
        in Span<float> results,
        in Span<float> groupAlphas,
        int componentCount)
    {
        int pixelStride = componentCount + 1;
        int termStep = sourceTerms.Step * componentCount;
        ref float result = ref MemoryMarshal.GetReference(results);
        ref float backdrop = ref MemoryMarshal.GetReference(backdrops);
        ref float groupAlpha = ref MemoryMarshal.GetReference(groupAlphas);
        ref float term = ref MemoryMarshal.GetReference(sourceTerms.Values);
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
            float sourceShape = row.GetSourceShape(coverage, shape, mask);

            if (sourceShape > 0)
            {
                float sourceAlpha = row.GetSourceAlpha(coverage, alpha, mask);
                float keep = 1 - sourceShape;
                float backdropWeight = sourceShape - sourceAlpha;

                for (int component = 0; component < componentCount; component++)
                {
                    ref float value = ref Unsafe.Add(ref result, component);
                    float backdropTerm = backdropWeight * Unsafe.Add(ref backdrop, component);
                    value = (keep * value) + backdropTerm + (sourceAlpha * Unsafe.Add(ref term, component));
                }

                ref float resultAlpha = ref Unsafe.Add(ref result, componentCount);
                float alphaBackdropTerm = backdropWeight * Unsafe.Add(ref backdrop, componentCount);
                resultAlpha = (keep * resultAlpha) + alphaBackdropTerm + sourceAlpha;
                groupAlpha = (keep * groupAlpha) + sourceAlpha;
            }

            result = ref Unsafe.Add(ref result, pixelStride);
            backdrop = ref Unsafe.Add(ref backdrop, pixelStride);
            groupAlpha = ref Unsafe.Add(ref groupAlpha, 1);
            term = ref Unsafe.Add(ref term, termStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            alpha = ref Unsafe.Add(ref alpha, alphaStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

    /// <summary>
    /// Writes the source term S = (1 − αb)·Cs + αb·B(Cb, Cs) of every covered pixel, for a blend mode other than Normal.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void ComputeSourceTerms(
        PdfBlendMode blendMode,
        in SnapSourceRow row,
        in ReadOnlySpan<float> backdrops,
        in Span<float> backdropColors,
        in Span<float> sourceTerms,
        PdfColorSpaceConverter colorSpace)
    {
        int componentCount = colorSpace.Components;
        int pixelStride = componentCount + 1;
        int count = row.Count;
        ref float backdrop = ref MemoryMarshal.GetReference(backdrops);
        ref float backdropColor = ref MemoryMarshal.GetReference(backdropColors);

        for (int index = 0; index < count; index++)
        {
            float backdropAlpha = Unsafe.Add(ref backdrop, componentCount);
            float inverseAlpha = (backdropAlpha > 0) ? 1 / backdropAlpha : 0;

            for (int component = 0; component < componentCount; component++)
            {
                Unsafe.Add(ref backdropColor, component) = Unsafe.Add(ref backdrop, component) * inverseAlpha;
            }

            backdrop = ref Unsafe.Add(ref backdrop, pixelStride);
            backdropColor = ref Unsafe.Add(ref backdropColor, componentCount);
        }

        SnapBlendFunctions.BlendRow(blendMode, backdropColors, row.Color, row.Coverage, sourceTerms, colorSpace);

        int colorStep = row.Color.Step * componentCount;
        backdrop = ref MemoryMarshal.GetReference(backdrops);
        ref float term = ref MemoryMarshal.GetReference(sourceTerms);
        ref float color = ref MemoryMarshal.GetReference(row.Color.Values);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        int coverageStep = row.Coverage.Step;

        for (int index = 0; index < count; index++)
        {
            if (coverage > 0)
            {
                float backdropAlpha = Unsafe.Add(ref backdrop, componentCount);
                float sourceWeight = 1 - backdropAlpha;

                for (int component = 0; component < componentCount; component++)
                {
                    ref float value = ref Unsafe.Add(ref term, component);
                    value = (sourceWeight * Unsafe.Add(ref color, component)) + (backdropAlpha * value);
                }
            }

            backdrop = ref Unsafe.Add(ref backdrop, pixelStride);
            term = ref Unsafe.Add(ref term, componentCount);
            color = ref Unsafe.Add(ref color, colorStep);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
        }
    }

    /// <summary>
    /// Adds the element's shape to the group's: fg′ = fg ∪ fs.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void AccumulateShapes(in SnapSourceRow row, in Span<float> groupShapes)
    {
        ref float groupShape = ref MemoryMarshal.GetReference(groupShapes);
        ref float coverage = ref MemoryMarshal.GetReference(row.Coverage.Values);
        ref float shape = ref MemoryMarshal.GetReference(row.Shape.Values);
        ref float mask = ref MemoryMarshal.GetReference(row.Mask.Values);
        int coverageStep = row.Coverage.Step;
        int shapeStep = row.Shape.Step;
        int maskStep = row.Mask.Step;

        for (int index = 0; index < row.Count; index++)
        {
            groupShape = Union(groupShape, row.GetSourceShape(coverage, shape, mask));

            groupShape = ref Unsafe.Add(ref groupShape, 1);
            coverage = ref Unsafe.Add(ref coverage, coverageStep);
            shape = ref Unsafe.Add(ref shape, shapeStep);
            mask = ref Unsafe.Add(ref mask, maskStep);
        }
    }

    /// <summary>
    /// Writes a non-isolated group's result with its initial backdrop removed: Pg = Pn − (1 − αg)·P₀, alpha αg.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static void FactorOutBackdrop(
        in ReadOnlySpan<float> results,
        in ReadOnlySpan<float> backdrops,
        in ReadOnlySpan<float> groupAlphas,
        in Span<float> values,
        int componentCount)
    {
        int pixelStride = componentCount + 1;
        ref float result = ref MemoryMarshal.GetReference(results);
        ref float backdrop = ref MemoryMarshal.GetReference(backdrops);
        ref float groupAlpha = ref MemoryMarshal.GetReference(groupAlphas);
        ref float value = ref MemoryMarshal.GetReference(values);

        for (int index = 0; index < groupAlphas.Length; index++)
        {
            float backdropWeight = 1 - groupAlpha;

            for (int component = 0; component < componentCount; component++)
            {
                Unsafe.Add(ref value, component) = Unsafe.Add(ref result, component) - (backdropWeight * Unsafe.Add(ref backdrop, component));
            }

            Unsafe.Add(ref value, componentCount) = groupAlpha;

            result = ref Unsafe.Add(ref result, pixelStride);
            backdrop = ref Unsafe.Add(ref backdrop, pixelStride);
            groupAlpha = ref Unsafe.Add(ref groupAlpha, 1);
            value = ref Unsafe.Add(ref value, pixelStride);
        }
    }

    private static float Union(float backdrop, float source) => backdrop + source - (backdrop * source);
}
