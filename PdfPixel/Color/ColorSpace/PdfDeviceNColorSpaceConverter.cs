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
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction? _tintFunction;

    /// <summary>
    /// Initializes the space from its colorant names, alternate color space, and tint transform function.
    /// </summary>
    public PdfDeviceNColorSpaceConverter(PdfString?[]? componentNames, PdfColorSpaceConverter? alternate, PdfFunction? tintFunction)
    {
        Names = componentNames ?? Array.Empty<PdfString?>();
        _alternate = alternate ?? PdfDeviceRgbColorSpaceConverter.Instance;
        _tintFunction = tintFunction;
    }

    /// <summary>
    /// Gets the names of the colorants this space's components are tints of.
    /// </summary>
    public IReadOnlyList<PdfString?> Names { get; }

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
        ColorTransformSampler alternateSampler = _alternate.GetRgbaSampler(parameters, normalize: true);

        if (_tintFunction == null)
        {
            return alternateSampler;
        }

        return new ColorTransformSampler(alternateSampler.ColorTransform, _tintFunction.Evaluate);
    }
}
