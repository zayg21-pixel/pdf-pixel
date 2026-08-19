using PdfPixel.Color.Icc;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The CalRGB color space: red, green, and blue components defined by CIE colorimetry rather than
/// by the output device.
/// </summary>
public class PdfCalRgbColorSpaceConverter : PdfColorSpaceConverter
{
    /// <summary>
    /// Initializes the space from its white point, black point, per-component gamma, and the matrix
    /// mapping its components to CIE XYZ.
    /// </summary>
    public PdfCalRgbColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float[]? gamma, float[,]? matrix3x3)
    {
        // TODO: [LOW] Handle blackPoint if needed, it's unused currently and seems to be ignored by all major PDF viewers.
        Vector4 whitePointVector;

        if (whitePoint?.Length >= 3)
        {
            whitePointVector = ColorVectorUtilities.ToVector4WithOnePadding(whitePoint);
        }
        else
        {
            whitePointVector = IccTransforms.D65WhitePoint;
        }

        if (gamma == null || gamma.Length < 3)
        {
            gamma = new float[] { 1.0f, 1.0f, 1.0f };
        }

        matrix3x3 ??= new float[3, 3]
        {
            { 0, 0, 1 },
            { 0, 1, 0 },
            { 1, 0, 0 }
        };

        PerChannelTrcTransform trcTransform = new([IccTrc.FromGamma(gamma[0]), IccTrc.FromGamma(gamma[1]), IccTrc.FromGamma(gamma[2])]);

        Matrix4x4 chadMatrix = IccTransforms.BuildBradfordAdaptMatrix(whitePointVector, IccTransforms.D50WhitePoint);
        Matrix4x4 primariesMatrix = ColorVectorUtilities.ToMatrix4x4(matrix3x3);
        primariesMatrix = Matrix4x4.Transpose(primariesMatrix); // this matches how PDF specifies the matrix

        Matrix4x4 adaptedMatrix = Matrix4x4.Multiply(chadMatrix, primariesMatrix);
        adaptedMatrix = Matrix4x4.Transpose(adaptedMatrix);

        MatrixColorTransform matrixTransform = new(adaptedMatrix);

        ToSrgbTransform = new ChainedColorTransform(trcTransform, matrixTransform, IccTransforms.XyzD50ToSrgbTransform);
    }

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <summary>
    /// Gets the transform taking this space's components to sRGB.
    /// </summary>
    protected ChainedColorTransform ToSrgbTransform { get; }

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => new(new ChainedColorTransform(ToSrgbTransform, postTransform));
}
