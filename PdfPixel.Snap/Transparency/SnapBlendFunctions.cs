using PdfPixel.Color.ColorSpace;
using PdfPixel.Snap.Painting;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// The blend functions B(Cb, Cs) of ISO 32000-2 11.3.5; subtractive components are complemented before and after
/// (11.3.4).
/// </summary>
internal static class SnapBlendFunctions
{
    private interface ISeparableBlendFunction
    {
        float Apply(float backdrop, float source);
    }

    private interface INonSeparableBlendFunction
    {
        void Apply(in ReadOnlySpan<float> backdrop, in ReadOnlySpan<float> source, in Span<float> saturated, in Span<float> result);
    }

    /// <summary>
    /// Returns whether <paramref name="blendMode"/> acts as Normal, B(Cb, Cs) = Cs.
    /// </summary>
    public static bool IsNormal(PdfBlendMode blendMode)
    {
        switch (blendMode)
        {
            case PdfBlendMode.Unknown:
            case PdfBlendMode.Normal:
            case PdfBlendMode.Compatible:
            {
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Writes B(Cb, Cs) to <paramref name="results"/> for every pixel of a row whose shape is above zero.
    /// </summary>
    public static void BlendRow(
        PdfBlendMode blendMode,
        in ReadOnlySpan<float> backdrops,
        in SnapRowValues sources,
        in SnapRowValues shapes,
        in Span<float> results,
        PdfColorSpaceConverter colorSpace)
    {
        SnapBlendingColorModel colorModel = colorSpace.GetBlendingColorModel();

        if (IsNonSeparable(blendMode))
        {
            BlendNonSeparableRow(blendMode, backdrops, sources.Values, sources.Step, shapes, results, colorModel);
        }
        else
        {
            BlendSeparableRow(blendMode, backdrops, sources.Values, sources.Step, shapes, results, colorSpace.Components, colorModel == SnapBlendingColorModel.Cmyk);
        }
    }

    private static bool IsNonSeparable(PdfBlendMode blendMode)
    {
        switch (blendMode)
        {
            case PdfBlendMode.Hue:
            case PdfBlendMode.Saturation:
            case PdfBlendMode.Color:
            case PdfBlendMode.Luminosity:
            {
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    private static void BlendSeparableRow(
        PdfBlendMode blendMode,
        in ReadOnlySpan<float> backdrops,
        in ReadOnlySpan<float> sources,
        int sourceStep,
        in SnapRowValues shapes,
        in Span<float> results,
        int componentCount,
        bool isSubtractive)
    {
        switch (blendMode)
        {
            case PdfBlendMode.Multiply:
            {
                BlendSeparableRow<MultiplyFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.Screen:
            {
                BlendSeparableRow<ScreenFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.Overlay:
            {
                BlendSeparableRow<OverlayFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.Darken:
            {
                BlendSeparableRow<DarkenFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.Lighten:
            {
                BlendSeparableRow<LightenFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.ColorDodge:
            {
                BlendSeparableRow<ColorDodgeFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.ColorBurn:
            {
                BlendSeparableRow<ColorBurnFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.HardLight:
            {
                BlendSeparableRow<HardLightFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.SoftLight:
            {
                BlendSeparableRow<SoftLightFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.Difference:
            {
                BlendSeparableRow<DifferenceFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            case PdfBlendMode.Exclusion:
            {
                BlendSeparableRow<ExclusionFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
            default:
            {
                BlendSeparableRow<SourceFunction>(backdrops, sources, sourceStep, shapes, results, componentCount, isSubtractive);
                break;
            }
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void BlendSeparableRow<TFunction>(
        in ReadOnlySpan<float> backdrops,
        in ReadOnlySpan<float> sources,
        int sourceStep,
        in SnapRowValues shapes,
        in Span<float> results,
        int componentCount,
        bool isSubtractive)
        where TFunction : struct, ISeparableBlendFunction
    {
        TFunction function = default;
        float complement = isSubtractive ? 1f : 0f;
        float sign = isSubtractive ? -1f : 1f;
        int count = results.Length / componentCount;
        int sourceComponentStep = sourceStep * componentCount;
        int shapeStep = shapes.Step;
        ref float backdrop = ref MemoryMarshal.GetReference(backdrops);
        ref float source = ref MemoryMarshal.GetReference(sources);
        ref float result = ref MemoryMarshal.GetReference(results);
        ref float shape = ref MemoryMarshal.GetReference(shapes.Values);

        for (int index = 0; index < count; index++)
        {
            if (shape > 0)
            {
                // An additive value is the value itself, a subtractive one its complement: complement + sign·value.
                for (int component = 0; component < componentCount; component++)
                {
                    float backdropValue = complement + (sign * Unsafe.Add(ref backdrop, component));
                    float sourceValue = complement + (sign * Unsafe.Add(ref source, component));
                    Unsafe.Add(ref result, component) = complement + (sign * function.Apply(backdropValue, sourceValue));
                }
            }

            backdrop = ref Unsafe.Add(ref backdrop, componentCount);
            source = ref Unsafe.Add(ref source, sourceComponentStep);
            result = ref Unsafe.Add(ref result, componentCount);
            shape = ref Unsafe.Add(ref shape, shapeStep);
        }
    }

    private static void BlendNonSeparableRow(
        PdfBlendMode blendMode,
        in ReadOnlySpan<float> backdrops,
        in ReadOnlySpan<float> sources,
        int sourceStep,
        in SnapRowValues shapes,
        in Span<float> results,
        SnapBlendingColorModel colorModel)
    {
        switch (blendMode)
        {
            case PdfBlendMode.Hue:
            {
                BlendNonSeparableRow<HueFunction>(backdrops, sources, sourceStep, shapes, results, colorModel,useSourceBlack: false);
                break;
            }
            case PdfBlendMode.Saturation:
            {
                BlendNonSeparableRow<SaturationFunction>(backdrops, sources, sourceStep, shapes, results, colorModel,useSourceBlack: false);
                break;
            }
            case PdfBlendMode.Color:
            {
                BlendNonSeparableRow<ColorFunction>(backdrops, sources, sourceStep, shapes, results, colorModel,useSourceBlack: false);
                break;
            }
            default:
            {
                BlendNonSeparableRow<LuminosityFunction>(backdrops, sources, sourceStep, shapes, results, colorModel,useSourceBlack: true);
                break;
            }
        }
    }

    /// <summary>
    /// Applies a non-separable blend function by the RGB formulas of 11.3.5.3: gray blends as equal RGB and returns by
    /// luminosity; CMYK blends complemented CMY as RGB and takes K from the backdrop, or the source when
    /// <paramref name="useSourceBlack"/> is set.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static void BlendNonSeparableRow<TFunction>(
        in ReadOnlySpan<float> backdrops,
        in ReadOnlySpan<float> sources,
        int sourceStep,
        in SnapRowValues shapes,
        in Span<float> results,
        SnapBlendingColorModel colorModel,
        bool useSourceBlack)
        where TFunction : struct, INonSeparableBlendFunction
    {
        TFunction function = default;
        Span<float> backdrop = stackalloc float[3];
        Span<float> source = stackalloc float[3];
        Span<float> saturated = stackalloc float[3];
        Span<float> blended = stackalloc float[3];
        int componentCount = GetComponentCount(colorModel);
        int count = results.Length / componentCount;
        int sourceComponentStep = sourceStep * componentCount;
        int shapeStep = shapes.Step;
        ref float shape = ref MemoryMarshal.GetReference(shapes.Values);
        int offset = 0;
        int sourceOffset = 0;

        for (int index = 0; index < count; index++)
        {
            if (shape > 0)
            {
                ReadOnlySpan<float> backdropColor = backdrops.Slice(offset, componentCount);
                ReadOnlySpan<float> sourceColor = sources.Slice(sourceOffset, componentCount);
                Span<float> result = results.Slice(offset, componentCount);

                if (colorModel == SnapBlendingColorModel.Rgb)
                {
                    function.Apply(backdropColor, sourceColor, saturated, result);
                }
                else
                {
                    LoadRgb(backdropColor, colorModel, backdrop);
                    LoadRgb(sourceColor, colorModel, source);
                    function.Apply(backdrop, source, saturated, blended);
                    StoreRgb(blended, colorModel, result);
                }

                if (colorModel == SnapBlendingColorModel.Cmyk)
                {
                    result[3] = useSourceBlack ? sourceColor[3] : backdropColor[3];
                }
            }

            offset += componentCount;
            sourceOffset += sourceComponentStep;
            shape = ref Unsafe.Add(ref shape, shapeStep);
        }
    }

    private static int GetComponentCount(SnapBlendingColorModel colorModel)
    {
        switch (colorModel)
        {
            case SnapBlendingColorModel.Gray:
            {
                return 1;
            }
            case SnapBlendingColorModel.Rgb:
            {
                return 3;
            }
            default:
            {
                return 4;
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void LoadRgb(in ReadOnlySpan<float> color, SnapBlendingColorModel colorModel, in Span<float> rgb)
    {
        if (colorModel == SnapBlendingColorModel.Gray)
        {
            rgb.Fill(color[0]);
        }
        else
        {
            for (int component = 0; component < 3; component++)
            {
                rgb[component] = 1 - color[component];
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void StoreRgb(in ReadOnlySpan<float> rgb, SnapBlendingColorModel colorModel, in Span<float> color)
    {
        if (colorModel == SnapBlendingColorModel.Gray)
        {
            color[0] = GetLuminosity(rgb);
        }
        else
        {
            for (int component = 0; component < 3; component++)
            {
                color[component] = 1 - rgb[component];
            }
        }
    }

    private static float Screen(float backdrop, float source) => backdrop + source - (backdrop * source);

    private static float HardLight(float backdrop, float source)
    {
        if (source <= 0.5f)
        {
            return backdrop * 2 * source;
        }

        return Screen(backdrop, (2 * source) - 1);
    }

    private static float SoftLight(float backdrop, float source)
    {
        if (source <= 0.5f)
        {
            return backdrop - ((1 - (2 * source)) * backdrop * (1 - backdrop));
        }

        float darkened = (backdrop <= 0.25f)
            ? ((((16 * backdrop) - 12) * backdrop) + 4) * backdrop
            : (float)Math.Sqrt(backdrop);

        return backdrop + (((2 * source) - 1) * (darkened - backdrop));
    }

    private static float GetLuminosity(in ReadOnlySpan<float> color) => (0.3f * color[0]) + (0.59f * color[1]) + (0.11f * color[2]);

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void SetLuminosity(in ReadOnlySpan<float> color, float luminosity, in Span<float> result)
    {
        float delta = luminosity - GetLuminosity(color);

        for (int component = 0; component < 3; component++)
        {
            result[component] = color[component] + delta;
        }

        ClipColor(result);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ClipColor(in Span<float> color)
    {
        float luminosity = GetLuminosity(color);
        float minimum = Math.Min(color[0], Math.Min(color[1], color[2]));
        float maximum = Math.Max(color[0], Math.Max(color[1], color[2]));

        for (int component = 0; component < 3; component++)
        {
            if (minimum < 0)
            {
                color[component] = luminosity + ((color[component] - luminosity) * luminosity / (luminosity - minimum));
            }

            if (maximum > 1)
            {
                color[component] = luminosity + ((color[component] - luminosity) * (1 - luminosity) / (maximum - luminosity));
            }
        }
    }

    private static float GetSaturation(in ReadOnlySpan<float> color)
        => Math.Max(color[0], Math.Max(color[1], color[2])) - Math.Min(color[0], Math.Min(color[1], color[2]));

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void SetSaturation(in ReadOnlySpan<float> color, float saturation, in Span<float> result)
    {
        float minimum = Math.Min(color[0], Math.Min(color[1], color[2]));
        float maximum = Math.Max(color[0], Math.Max(color[1], color[2]));
        float range = maximum - minimum;

        for (int component = 0; component < 3; component++)
        {
            // The spec's Cmid formula, applied to every component: the maximum lands on s, the minimum on 0.
            result[component] = (range > 0) ? (color[component] - minimum) * (saturation / range) : 0;
        }
    }

    private readonly struct SourceFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => source;
    }

    private readonly struct MultiplyFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => backdrop * source;
    }

    private readonly struct ScreenFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => Screen(backdrop, source);
    }

    private readonly struct OverlayFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => HardLight(source, backdrop);
    }

    private readonly struct DarkenFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => Math.Min(backdrop, source);
    }

    private readonly struct LightenFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => Math.Max(backdrop, source);
    }

    private readonly struct ColorDodgeFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source)
        {
            if (backdrop <= 0)
            {
                return 0;
            }

            return (source >= 1) ? 1 : Math.Min(1, backdrop / (1 - source));
        }
    }

    private readonly struct ColorBurnFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source)
        {
            if (backdrop >= 1)
            {
                return 1;
            }

            return (source <= 0) ? 0 : 1 - Math.Min(1, (1 - backdrop) / source);
        }
    }

    private readonly struct HardLightFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => HardLight(backdrop, source);
    }

    private readonly struct SoftLightFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => SoftLight(backdrop, source);
    }

    private readonly struct DifferenceFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => Math.Abs(backdrop - source);
    }

    private readonly struct ExclusionFunction : ISeparableBlendFunction
    {
        public float Apply(float backdrop, float source) => backdrop + source - (2 * backdrop * source);
    }

    private readonly struct HueFunction : INonSeparableBlendFunction
    {
        public void Apply(in ReadOnlySpan<float> backdrop, in ReadOnlySpan<float> source, in Span<float> saturated, in Span<float> result)
        {
            SetSaturation(source, GetSaturation(backdrop), saturated);
            SetLuminosity(saturated, GetLuminosity(backdrop), result);
        }
    }

    private readonly struct SaturationFunction : INonSeparableBlendFunction
    {
        public void Apply(in ReadOnlySpan<float> backdrop, in ReadOnlySpan<float> source, in Span<float> saturated, in Span<float> result)
        {
            SetSaturation(backdrop, GetSaturation(source), saturated);
            SetLuminosity(saturated, GetLuminosity(backdrop), result);
        }
    }

    private readonly struct ColorFunction : INonSeparableBlendFunction
    {
        public void Apply(in ReadOnlySpan<float> backdrop, in ReadOnlySpan<float> source, in Span<float> saturated, in Span<float> result)
            => SetLuminosity(source, GetLuminosity(backdrop), result);
    }

    private readonly struct LuminosityFunction : INonSeparableBlendFunction
    {
        public void Apply(in ReadOnlySpan<float> backdrop, in ReadOnlySpan<float> source, in Span<float> saturated, in Span<float> result)
            => SetLuminosity(backdrop, GetLuminosity(source), result);
    }
}
