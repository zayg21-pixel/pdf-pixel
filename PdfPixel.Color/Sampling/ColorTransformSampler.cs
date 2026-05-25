using PdfPixel.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Sampling;

public delegate ReadOnlySpan<float> SpanConverter(ReadOnlySpan<float> source);

/// <summary>
/// Samples a <see cref="ChainedColorTransform"/> into a <see cref="Vector4"/> color value.
/// An optional <see cref="SpanConverter"/> can override the default <see cref="ColorVectorUtilities.ToVector4WithOnePadding"/>
/// pre-processing step — used for tint functions in Separation and DeviceN color spaces.
/// </summary>
public sealed class ColorTransformSampler
{
    private readonly ChainedColorTransform _colorTransform;
    private readonly SpanConverter _sourceOverride;

    public ColorTransformSampler(ChainedColorTransform chainedTransform, SpanConverter sourceOverride = null)
    {
        _colorTransform = chainedTransform;
        _sourceOverride = sourceOverride;
    }

    public ChainedColorTransform ColorTransform => _colorTransform;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Sample(ReadOnlySpan<float> source)
    {
        if (_sourceOverride != null)
        {
            source = _sourceOverride(source);
        }

        return _colorTransform.Transform(ColorVectorUtilities.ToVector4WithOnePadding(source));
    }
}
