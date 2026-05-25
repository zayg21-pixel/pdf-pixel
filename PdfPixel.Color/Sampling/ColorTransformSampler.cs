using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Sampling;

/// <summary>
/// Samples a <see cref="ChainedColorTransform"/> into a <see cref="Vector4"/> color value.
/// An optional <see cref="SpanConverter"/> can override the default <see cref="ColorVectorUtilities.ToVector4WithOnePadding"/>
/// pre-processing step — used for tint functions in Separation and DeviceN color spaces.
/// </summary>
public sealed class ColorTransformSampler
{
    private readonly SpanConverter? _sourceOverride;

    /// <summary>
    /// Initializes a new <see cref="ColorTransformSampler"/> with the given transform and an optional
    /// span pre-processor. When <paramref name="sourceOverride"/> is provided it replaces the default
    /// <see cref="ColorVectorUtilities.ToVector4WithOnePadding"/> conversion step.
    /// </summary>
    public ColorTransformSampler(ChainedColorTransform chainedTransform, SpanConverter? sourceOverride = null)
    {
        ColorTransform = chainedTransform;
        _sourceOverride = sourceOverride;
    }

    /// <summary>
    /// The color transform pipeline applied during sampling.
    /// </summary>
    public ChainedColorTransform ColorTransform { get; }

    /// <summary>
    /// Converts <paramref name="source"/> color components to a <see cref="Vector4"/> by running
    /// the optional pre-processor and then the full transform pipeline.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Sample(in ReadOnlySpan<float> source)
    {
        if (_sourceOverride != null)
        {
            return ColorTransform.Transform(ColorVectorUtilities.ToVector4WithOnePadding(_sourceOverride(source)));
        }
        else
        {
            return ColorTransform.Transform(ColorVectorUtilities.ToVector4WithOnePadding(source));
        }
    }
}
