using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Objects;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Separation color space: color values are tints of a single named colorant.
/// </summary>
public sealed class PdfSeparationColorSpaceConverter : PdfColorSpaceConverter
{
    internal PdfSeparationColorSpaceConverter(PdfString? name, PdfColorSpaceConverter? alternateSpace, PdfFunction? tintTransform)
    {
        Name = name;
        AlternateSpace = alternateSpace ?? PdfDeviceGrayColorSpaceConverter.Instance;
        TintTransform = tintTransform;
    }

    /// <summary>
    /// Gets the name of the colorant this space's tints apply to (name).
    /// </summary>
    public PdfString? Name { get; }

    /// <summary>
    /// Gets the alternate color space (alternateSpace), or DeviceGray when absent.
    /// </summary>
    public PdfColorSpaceConverter AlternateSpace { get; }

    /// <summary>
    /// Gets the function mapping a tint to the alternate color space (tintTransform), or <see langword="null"/> when absent.
    /// </summary>
    public PdfFunction? TintTransform { get; }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    internal override float[] GetInitialComponents() => [1f];

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
