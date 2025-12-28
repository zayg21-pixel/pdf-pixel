using PdfReader.Color.Icc;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Sampling;
using PdfReader.Color.Transform;
using System.Numerics;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Converter for PDF CalRGB (CIEBasedCalRGB) color space.
/// </summary>
internal class CalRgbConverter : PdfColorSpaceConverter
{
    public CalRgbConverter(float[] whitePoint, float[] blackPoint, float[] gamma, float[,] matrix3x3)
    {
        // TODO: Handle blackPoint if needed, it's unused currently and seems to be ignored by all major PDF viewers.
        Vector4 whitePointVector;

        if (whitePoint != null && whitePoint.Length >= 3)
        {
            whitePointVector = ColorVectorUtilities.ToVector4WithOnePadding(whitePoint);
        }
        else
        {
            whitePointVector = IccTransforms.D65WhitePoint;
        }

        if (gamma == null || gamma.Length < 3)
        {
            gamma = [1.0f, 1.0f, 1.0f];
        }

        matrix3x3 ??= new float[3, 3]
        {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 }
        };

        var trcTransform = new PerChannelTrcTransform([IccTrc.FromGamma(gamma[0]), IccTrc.FromGamma(gamma[1]), IccTrc.FromGamma(gamma[2])]);

        var chadMatrix = IccTransforms.BuildBradfordAdaptMatrix(whitePointVector, IccTransforms.D50WhitePoint);
        var primariesMatrix = ColorVectorUtilities.ToMatrix4x4(matrix3x3);
        primariesMatrix = Matrix4x4.Transpose(primariesMatrix); // this matches how PDF specifies the matrix

        var adaptedMatrix = Matrix4x4.Multiply(chadMatrix, primariesMatrix);
        adaptedMatrix = Matrix4x4.Transpose(adaptedMatrix);

        var matrixTransform = new MatrixColorTransform(adaptedMatrix);

        ToSrgbTransform = new ChainedColorTransform(trcTransform, matrixTransform, IccTransforms.XyzD50ToSrgbTransform);
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    protected ChainedColorTransform ToSrgbTransform { get; }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        return new ColorTransformSampler(ToSrgbTransform);
    }
}
