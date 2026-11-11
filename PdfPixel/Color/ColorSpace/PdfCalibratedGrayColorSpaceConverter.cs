using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The CalGray color space: a single gray component defined by CIE colorimetry rather than by the
/// output device.
/// </summary>
public sealed class PdfCalibratedGrayColorSpaceConverter : PdfCalibratedColorSpaceConverter
{
    /// <summary>
    /// Initializes a CalGray color space.
    /// </summary>
    /// <param name="whitePoint">The diffuse white point in CIE XYZ, or <see langword="null"/> for D65.</param>
    /// <param name="blackPoint">The diffuse black point in CIE XYZ, or <see langword="null"/> for [0 0 0].</param>
    /// <param name="gamma">The gamma of the gray component, or <see langword="null"/> for 1.</param>
    public PdfCalibratedGrayColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float? gamma)
        : this(GetWhitePointOrDefault(whitePoint), GetBlackPointOrDefault(blackPoint), gamma ?? 1f)
    {
    }

    private PdfCalibratedGrayColorSpaceConverter(float[] whitePoint, float[] blackPoint, float gamma)
        : base(whitePoint, blackPoint, [gamma, gamma, gamma], [whitePoint[0], 0f, 0f, 0f, whitePoint[1], 0f, 0f, 0f, whitePoint[2]])
    {
        Gamma = gamma;
    }

    /// <summary>
    /// Gets the gamma of the gray component (/Gamma), or 1 when absent.
    /// </summary>
    public float Gamma { get; }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override ChainedColorTransform GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
        => new(new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)), GetToXyzTransform(renderingIntent));

    /// <inheritdoc />
    public override ChainedColorTransform GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
        => GetFromXyzTransform(renderingIntent);
}
