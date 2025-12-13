using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Transform;
using PdfReader.Color.Icc.Utilities;
using PdfReader.Color.Lut;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Converter for PDF CalRGB (CIEBasedCalRGB) color space.
/// </summary>
internal sealed class CalRgbConverter : PdfColorSpaceConverter
{
    private readonly bool _hasBlackPoint;
    private readonly IIccTransform _toSrgbTransform;

    public CalRgbConverter(float[] whitePoint, float[] blackPoint, float[] gamma, float[,] matrix3x3)
    {
        Vector4 whitePointVector;

        if (whitePoint != null && whitePoint.Length >= 3)
        {
            whitePointVector = IccVectorUtilities.ToVector4(whitePoint);
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

        var trcTransform = new IccPerChannelLutTransform([IccTrc.FromGamma(gamma[0]), IccTrc.FromGamma(gamma[1]), IccTrc.FromGamma(gamma[2])]);

        var chadMatrix = IccTransforms.BuildBradfordAdaptMatrix(whitePointVector, IccTransforms.D50WhitePoint);
        var primariesMatrix = IccVectorUtilities.ToMatrix4x4(matrix3x3);
        primariesMatrix = Matrix4x4.Transpose(primariesMatrix); // this matches how PDF specifies the matrix

        var adaptedMatrix = Matrix4x4.Multiply(chadMatrix, primariesMatrix);
        adaptedMatrix = Matrix4x4.Transpose(adaptedMatrix);

        var matrixTransform = new IccMatrixTransform(adaptedMatrix);

        _toSrgbTransform = new IccChainedTransform(trcTransform, matrixTransform, IccTransforms.XyzD50ToSrgbTransform);
    }

    public override int Components => 3;

    public override bool IsDevice => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        var result = IccVectorUtilities.ToVector4(comps01);
        _toSrgbTransform.Transform(ref result);

        byte R = ToByte(result.X);
        byte G = ToByte(result.Y);
        byte B = ToByte(result.Z);

        return new SKColor(R, G, B);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        return TreeDLut.Build(intent, ToSrgbCore);
    }
}
