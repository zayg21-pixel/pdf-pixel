using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Pattern color space: color values are patterns, tiling or shading, instead of components.
/// </summary>
public sealed class PdfPatternColorSpaceConverter : PdfColorSpaceConverter
{
    internal PdfPatternColorSpaceConverter(PdfColorSpaceConverter? underlyingColorSpace) => UnderlyingColorSpace = underlyingColorSpace;

    /// <summary>
    /// Gets the color space an uncolored pattern's tint is given in, or <see langword="null"/> when absent.
    /// </summary>
    public PdfColorSpaceConverter? UnderlyingColorSpace { get; }

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override int Components => UnderlyingColorSpace?.Components ?? 0;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
        => UnderlyingColorSpace?.GetRgbaSampler(parameters, normalize) ?? new ColorTransformSampler(new ChainedColorTransform(parameters.TransferFunction));

    /// <inheritdoc />
    public override ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;
}
