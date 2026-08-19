using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The DeviceGray color space: one component giving the intensity of achromatic light, from black
/// at 0.0 to white at 1.0.
/// </summary>
public sealed class PdfDeviceGrayColorSpaceConverter : PdfColorSpaceConverter
{
    /// <summary>
    /// The shared instance of this color space.
    /// </summary>
    public static readonly PdfDeviceGrayColorSpaceConverter Instance = new();

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override bool IsDevice => true;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        ChainedColorTransform chained = new(new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)), postTransform);
        return new ColorTransformSampler(chained);
    }
}
