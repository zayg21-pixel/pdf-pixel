using PdfRender.Color.Sampling;
using PdfRender.Color.Transform;
using SkiaSharp;
using System;

namespace PdfRender.Color.ColorSpace;

/// <summary>
/// Base type for PDF color space converters producing sRGB output.
/// Implements IDisposable to release cached color filters.
/// </summary>
public abstract class PdfColorSpaceConverter
{
    private readonly IRgbaSampler[] _colorSamplers = new IRgbaSampler[Enum.GetValues(typeof(PdfRenderingIntent)).Length];

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
    public virtual SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent intent, IColorTransform postTransform)
    {
        return ColorVectorUtilities.From01ToSkiaColor(GetRgbaSampler(intent, postTransform).Sample(comps01));
    }

    /// <summary>
    /// Returns an RGBA sampler for the specified rendering intent.
    /// </summary>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <returns>Sampler value.</returns>
    public IRgbaSampler GetRgbaSampler(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        if (postTransform == null && _colorSamplers[(int)intent] is IRgbaSampler sampler)
        {
            return sampler;
        }
        var newSampler = GetRgbaSamplerCore(intent, postTransform);

        if (postTransform == null)
        {
            _colorSamplers[(int)intent] = newSampler;
            return newSampler;
        }
        else
        {
            return newSampler;
        }
    }

    /// <summary>
    /// Default implementation to create an RGBA sampler for the specified rendering intent.
    /// </summary>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <returns>RGBA sampler.</returns>
    protected abstract IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform);
}
