using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Pattern color space: color values are patterns, tiling or shading, instead of components.
/// </summary>
public sealed class PdfPatternColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter? _baseColorSpace;

    /// <summary>
    /// Initializes the space with the base color space an uncolored pattern's tint is given in.
    /// </summary>
    public PdfPatternColorSpaceConverter(PdfColorSpaceConverter? baseColorSpace) => _baseColorSpace = baseColorSpace;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override int Components => _baseColorSpace?.Components ?? 0;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => _baseColorSpace?.GetRgbaSampler(intent, postTransform, normalize) ?? new ColorTransformSampler(new ChainedColorTransform(postTransform));
}
