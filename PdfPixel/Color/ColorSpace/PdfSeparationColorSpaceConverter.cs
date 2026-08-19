using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Models;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Separation color space: color values are tints of a single named colorant.
/// </summary>
public sealed class PdfSeparationColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction? _tintFunction;

    /// <summary>
    /// Initializes the space from its colorant name, alternate color space, and tint transform function.
    /// </summary>
    public PdfSeparationColorSpaceConverter(PdfString? name, PdfColorSpaceConverter? alternate, PdfFunction? tintFunction)
    {
        Name = name;
        _alternate = alternate ?? PdfDeviceGrayColorSpaceConverter.Instance;
        _tintFunction = tintFunction;
    }

    /// <summary>
    /// Gets the name of the colorant this space's tints apply to.
    /// </summary>
    public PdfString? Name { get; }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        ColorTransformSampler alternateSampler = _alternate.GetRgbaSampler(intent, postTransform, normalize: true);

        if (_tintFunction == null)
        {
            return alternateSampler;
        }

        return new ColorTransformSampler(alternateSampler.ColorTransform, _tintFunction.Evaluate);
    }
}
