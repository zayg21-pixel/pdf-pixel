using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System;
using System.Collections.Generic;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Base type for PDF color space converters producing sRGB output.
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
    /// </summary>
    public abstract bool IsDevice { get; }

    /// <summary>
    /// Optional transform mapping this space's real component values into the ICC profile connection
    /// space encoding (0..1). Null when component values are already in that encoding, which covers
    /// most color spaces. Used by <see cref="PdfIccColorSpaceConverter"/> to correctly feed real component
    /// values of its Alternate into an embedded ICC profile transform.
    /// </summary>
    public virtual IColorTransform? NormalizeTransform => null;

    /// <summary>
    /// Converts normalized (0..1) component values to sRGB using the derived converter implementation.
    /// </summary>
    /// <param name="comps01">Component values in the range 0..1.</param>
    /// <param name="parameters">Color conversion parameters.</param>
    /// <returns>sRGB color.</returns>
    public virtual PdfColor ToSrgb(in ReadOnlySpan<float> comps01, in PdfColorConversionParameters parameters)
        => GetRgbaSampler(parameters).Sample(comps01).ToPdfColor();

    /// <summary>
    /// Returns an RGBA sampler for the specified color conversion parameters.
    /// </summary>
    /// <param name="parameters">Color conversion parameters.</param>
    /// <param name="normalize">
    /// True (the default) when the caller supplies this space's real,
    /// native component values (e.g. real Lab units) and expects the converter to range-clamp and
    /// re-encode them as needed internally. False when the caller already has values in whatever
    /// domain this converter's underlying pipeline consumes natively, so that step should be skipped.
    /// </param>
    /// <returns>Sampler value.</returns>
    public ColorTransformSampler GetRgbaSampler(in PdfColorConversionParameters parameters, bool normalize = true)
    {
        SamplerCacheKey key = new(parameters, normalize);
        if (_samplers.TryGetValue(key, out ColorTransformSampler? cachedSampler))
        {
            return cachedSampler;
        }

        ColorTransformSampler createdSampler = GetRgbaSamplerCore(parameters, normalize);
        _samplers[key] = createdSampler;
        return createdSampler;
    }

    /// <summary>
    /// Default implementation to create an RGBA sampler for the specified color conversion parameters.
    /// </summary>
    /// <param name="parameters">Color conversion parameters.</param>
    /// <param name="normalize">See <see cref="GetRgbaSampler"/>.</param>
    /// <returns>RGBA sampler.</returns>
    protected abstract ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize);

    private readonly struct SamplerCacheKey : IEquatable<SamplerCacheKey>
    {
        private readonly PdfColorConversionParameters _parameters;
        private readonly bool _normalize;

        public SamplerCacheKey(in PdfColorConversionParameters parameters, bool normalize)
        {
            _parameters = parameters;
            _normalize = normalize;
        }

        public bool Equals(SamplerCacheKey other)
            => _parameters == other._parameters && _normalize == other._normalize;

        public override bool Equals(object? obj) => obj is SamplerCacheKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(_parameters, _normalize);
    }
}
