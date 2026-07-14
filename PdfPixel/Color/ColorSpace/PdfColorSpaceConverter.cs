using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Base type for PDF color space converters producing sRGB output.
/// Implements IDisposable to release cached color filters.
/// </summary>
public abstract class PdfColorSpaceConverter
{
    private readonly Dictionary<PostTransformCacheKey, ColorTransformSampler> _postTransformSamplers = [];

#if !NETSTANDARD2_0
    private readonly ColorTransformSampler[] _colorSamplers = new ColorTransformSampler[Enum.GetValues<PdfRenderingIntent>().Length];

    static PdfColorSpaceConverter()
    {
        ChainedColorTransform.ResolveTransformType = static transform => transform switch
        {
            Transform.TransferFunctionTransform => typeof(Transform.TransferFunctionTransform),
            Transform.IndexedColorTransform => typeof(Transform.IndexedColorTransform),
            _ => null
        };
    }
#else
    private readonly ColorTransformSampler[] _colorSamplers = new ColorTransformSampler[Enum.GetValues(typeof(PdfRenderingIntent)).Length];
#endif

    /// <summary>
    /// Gets the number of input components for the color space (e.g. 1=Gray, 3=RGB, 4=CMYK).
    /// </summary>
    public abstract int Components { get; }

    /// <summary>
    /// Gets a value indicating whether this converter represents a device (native) color space.
    /// Device spaces may bypass certain lookups.
    /// </summary>
    public abstract bool IsDevice { get; }

    /// <summary>
    /// Converts normalized (0..1) component values to sRGB using the derived converter implementation.
    /// </summary>
    /// <param name="comps01">Component values in the range 0..1.</param>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <returns>sRGB color.</returns>
    public virtual SKColor ToSrgb(in ReadOnlySpan<float> comps01, PdfRenderingIntent intent, IColorTransform? postTransform)
        => GetRgbaSampler(intent, postTransform).Sample(comps01).From01ToSkiaColor();

    /// <summary>
    /// Returns an RGBA sampler for the specified rendering intent.
    /// </summary>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <returns>Sampler value.</returns>
    public ColorTransformSampler GetRgbaSampler(PdfRenderingIntent intent, IColorTransform? postTransform)
    {
        if (postTransform == null)
        {
            if (_colorSamplers[(int)intent] is ColorTransformSampler sampler)
            {
                return sampler;
            }

            ColorTransformSampler newSampler = GetRgbaSamplerCore(intent, postTransform);
            _colorSamplers[(int)intent] = newSampler;
            return newSampler;
        }

        PostTransformCacheKey key = new(intent, postTransform);
        if (_postTransformSamplers.TryGetValue(key, out ColorTransformSampler? cachedSampler))
        {
            return cachedSampler;
        }

        ColorTransformSampler createdSampler = GetRgbaSamplerCore(intent, postTransform);
        _postTransformSamplers[key] = createdSampler;
        return createdSampler;
    }

    /// <summary>
    /// Default implementation to create an RGBA sampler for the specified rendering intent.
    /// </summary>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <returns>RGBA sampler.</returns>
    protected abstract ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform? postTransform);

    private readonly struct PostTransformCacheKey : IEquatable<PostTransformCacheKey>
    {
        private readonly PdfRenderingIntent _intent;
        private readonly IColorTransform _postTransform;

        public PostTransformCacheKey(PdfRenderingIntent intent, IColorTransform postTransform)
        {
            _intent = intent;
            _postTransform = postTransform;
        }

        public bool Equals(PostTransformCacheKey other)
            => _intent == other._intent && ReferenceEquals(_postTransform, other._postTransform);

        public override bool Equals(object? obj) => obj is PostTransformCacheKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(_intent, RuntimeHelpers.GetHashCode(_postTransform));
    }
}
