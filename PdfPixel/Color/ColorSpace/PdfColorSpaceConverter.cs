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
    private readonly Dictionary<SamplerCacheKey, ColorTransformSampler> _samplers = [];

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
    /// Optional transform mapping this space's real component values into the ICC profile connection
    /// space encoding (0..1). Null when component values are already in that encoding, which covers
    /// most color spaces. Used by <see cref="IccBasedConverter"/> to correctly feed real component
    /// values of its Alternate into an embedded ICC profile transform.
    /// </summary>
    public virtual IColorTransform? NormalizeTransform => null;

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
    /// <param name="normalize">
    /// True (the default) when <paramref name="postTransform"/>'s caller supplies this space's real,
    /// native component values (e.g. real Lab units) and expects the converter to range-clamp and
    /// re-encode them as needed internally. False when the caller already has values in whatever
    /// domain this converter's underlying pipeline consumes natively, so that step should be skipped.
    /// </param>
    /// <returns>Sampler value.</returns>
    public ColorTransformSampler GetRgbaSampler(PdfRenderingIntent intent, IColorTransform? postTransform, bool normalize = true)
    {
        SamplerCacheKey key = new(intent, postTransform, normalize);
        if (_samplers.TryGetValue(key, out ColorTransformSampler? cachedSampler))
        {
            return cachedSampler;
        }

        ColorTransformSampler createdSampler = GetRgbaSamplerCore(intent, postTransform, normalize);
        _samplers[key] = createdSampler;
        return createdSampler;
    }

    /// <summary>
    /// Default implementation to create an RGBA sampler for the specified rendering intent.
    /// </summary>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <param name="normalize">See <see cref="GetRgbaSampler"/>.</param>
    /// <returns>RGBA sampler.</returns>
    protected abstract ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform? postTransform, bool normalize);

    private readonly struct SamplerCacheKey : IEquatable<SamplerCacheKey>
    {
        private readonly PdfRenderingIntent _intent;
        private readonly IColorTransform? _postTransform;
        private readonly bool _normalize;

        public SamplerCacheKey(PdfRenderingIntent intent, IColorTransform? postTransform, bool normalize)
        {
            _intent = intent;
            _postTransform = postTransform;
            _normalize = normalize;
        }

        public bool Equals(SamplerCacheKey other)
            => _intent == other._intent && _normalize == other._normalize && ReferenceEquals(_postTransform, other._postTransform);

        public override bool Equals(object? obj) => obj is SamplerCacheKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(_intent, _normalize, (_postTransform == null) ? 0 : RuntimeHelpers.GetHashCode(_postTransform));
    }
}
