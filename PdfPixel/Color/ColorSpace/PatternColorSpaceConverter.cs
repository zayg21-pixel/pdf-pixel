using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Converter placeholder for PDF Pattern color space.
/// Colored patterns: no base color space, components = 0 (color defined by pattern content stream).
/// Uncolored (stencil) tiling patterns: pattern provides shape, current color supplied via base color space components.
/// This converter only acts as a marker; actual pattern painting handled by pattern painting logic.
/// </summary>
internal sealed class PatternColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter? _baseColorSpace;

    public PatternColorSpaceConverter(PdfColorSpaceConverter? baseColorSpace) => _baseColorSpace = baseColorSpace; // may be null for colored patterns

    public override bool IsDevice => false;

    public override int Components => _baseColorSpace?.Components ?? 0;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => _baseColorSpace?.GetRgbaSampler(intent, postTransform, normalize) ?? new ColorTransformSampler(new ChainedColorTransform(postTransform));
}
