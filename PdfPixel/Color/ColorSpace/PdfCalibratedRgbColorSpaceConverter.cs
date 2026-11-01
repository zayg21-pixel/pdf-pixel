using PdfPixel.Color.Transform;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The CalRGB color space: red, green, and blue components defined by CIE colorimetry rather than
/// by the output device.
/// </summary>
public sealed class PdfCalibratedRgbColorSpaceConverter : PdfCalibratedColorSpaceConverter
{
    internal PdfCalibratedRgbColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float[]? gamma, float[]? matrix)
        : base(GetWhitePointOrDefault(whitePoint), GetBlackPointOrDefault(blackPoint), GetGammaOrDefault(gamma), GetMatrixOrDefault(matrix))
    {
        Gamma = GetGammaOrDefault(gamma);
        Matrix = GetMatrixOrDefault(matrix);
    }

    /// <summary>
    /// Gets the gamma of the red, green, and blue components (/Gamma), or [1 1 1] when absent.
    /// </summary>
    public float[] Gamma { get; }

    /// <summary>
    /// Gets the matrix mapping the decoded components to CIE XYZ (/Matrix), or the identity when absent.
    /// </summary>
    public float[] Matrix { get; }

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
        => new(new ChainedColorTransform(GetToSrgbTransform(parameters.RenderingIntent), parameters.TransferFunction));

    private static float[] GetGammaOrDefault(float[]? gamma)
    {
        if (gamma?.Length >= 3)
        {
            return gamma.AsSpan(0, 3).ToArray();
        }

        return new float[] { 1f, 1f, 1f };
    }

    private static float[] GetMatrixOrDefault(float[]? matrix)
    {
        if (matrix?.Length >= 9)
        {
            return matrix.AsSpan(0, 9).ToArray();
        }

        return new float[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
    }
}
