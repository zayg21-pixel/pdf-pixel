using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Objects;
using System;
using System.Collections.Generic;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The DeviceN color space: color values are tints of several named colorants, one per component.
/// </summary>
public sealed class PdfDeviceNColorSpaceConverter : PdfColorSpaceConverter
{
    internal PdfDeviceNColorSpaceConverter(PdfString?[]? names, PdfColorSpaceConverter? alternateSpace, PdfFunction? tintTransform)
    {
        Names = names ?? Array.Empty<PdfString?>();
        AlternateSpace = alternateSpace ?? PdfDeviceRgbColorSpaceConverter.Instance;
        TintTransform = tintTransform;
    }

    /// <summary>
    /// Gets the names of the colorants this space's components are tints of (names).
    /// </summary>
    public IReadOnlyList<PdfString?> Names { get; }

    /// <summary>
    /// Gets the alternate color space (alternateSpace), or DeviceRGB when absent.
    /// </summary>
    public PdfColorSpaceConverter AlternateSpace { get; }

    /// <summary>
    /// Gets the function mapping the tints to the alternate color space (tintTransform), or <see langword="null"/> when absent.
    /// </summary>
    public PdfFunction? TintTransform { get; }

    /// <inheritdoc />
    public override int Components => (Names.Count > 0) ? Names.Count : 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    internal override float[] GetInitialComponents()
    {
        var components = new float[Components];
        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            components[componentIndex] = 1f;
        }

        return components;
    }

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
    {
        ColorTransformSampler alternateSampler = AlternateSpace.GetRgbaSampler(parameters, normalize: true);

        if (TintTransform == null)
        {
            return alternateSampler;
        }

        return new ColorTransformSampler(alternateSampler.ColorTransform, TintTransform.Evaluate);
    }

    /// <inheritdoc />
    public override ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;
}
