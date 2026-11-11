using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The DeviceCMYK color space: four components giving the cyan, magenta, yellow, and black
/// subtractive colorants of the output device.
/// </summary>
public sealed class PdfDeviceCmykColorSpaceConverter : PdfColorSpaceConverter
{
    /// <summary>
    /// The shared instance of this color space.
    /// </summary>
    public static readonly PdfDeviceCmykColorSpaceConverter Instance = new();

    /// <inheritdoc />
    public override int Components => 4;

    /// <inheritdoc />
    public override bool IsDevice => true;

    /// <inheritdoc />
    internal override float[] GetInitialComponents() => [0f, 0f, 0f, 1f];

    /// <inheritdoc />
    public override ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;

    /// <inheritdoc />
    public override ChainedColorTransform? GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;
}
