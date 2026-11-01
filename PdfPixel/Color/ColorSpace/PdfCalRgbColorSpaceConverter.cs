using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;
using System;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The CalRGB color space: red, green, and blue components defined by CIE colorimetry rather than
/// by the output device.
/// </summary>
public class PdfCalRgbColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly ChainedColorTransform _toSrgbTransform;
    private readonly ChainedColorTransform _absoluteToSrgbTransform;

    /// <summary>
    /// Initializes the space from its white point, black point, per-component gamma, and the matrix
    /// mapping its components to CIE XYZ.
    /// </summary>
    public PdfCalRgbColorSpaceConverter(float[]? whitePoint, float[]? blackPoint, float[]? gamma, float[,]? matrix3x3)
    {
        Vector4 whitePointVector;

        if (whitePoint?.Length >= 3)
        {
            whitePointVector = ColorVectorUtilities.ToVector4WithOnePadding(whitePoint);
        }
        else
        {
            whitePointVector = IccTransforms.D65WhitePoint;
        }

        Vector4 blackPointVector = Vector4.Zero;

        if (blackPoint?.Length >= 3)
        {
            blackPointVector = ColorVectorUtilities.ToVector4WithZeroPadding(blackPoint.AsSpan(0, 3));
        }

        if (gamma == null || gamma.Length < 3)
        {
            gamma = new float[] { 1.0f, 1.0f, 1.0f };
        }

        matrix3x3 ??= new float[3, 3]
        {
            { 1, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 }
        };

        PerChannelTrcTransform trcTransform = new([IccTrc.FromGamma(gamma[0]), IccTrc.FromGamma(gamma[1]), IccTrc.FromGamma(gamma[2])]);

        Matrix4x4 chadMatrix = IccTransforms.BuildBradfordAdaptMatrix(whitePointVector, IccTransforms.D50WhitePoint);
        Matrix4x4 primariesMatrix = ColorVectorUtilities.ToMatrix4x4(matrix3x3);
        primariesMatrix = Matrix4x4.Transpose(primariesMatrix); // this matches how PDF specifies the matrix

        Matrix4x4 blackPointScaleMatrix = Matrix4x4.Identity;
        FunctionColorTransform? blackPointOffsetTransform = null;
        IColorTransform? blackPointCompensationTransform = null;

        // BlackPoint is the media black: the components decode into [BlackPoint, WhitePoint], then black point
        // compensation maps the neutral black at its lightness, at most L* 50, to zero; absolute colorimetry
        // skips the compensation.
        if (blackPointVector != Vector4.Zero)
        {
            Vector4 blackPointScale = (whitePointVector - blackPointVector) / whitePointVector;
            blackPointScaleMatrix = Matrix4x4.CreateScale(blackPointScale.X, blackPointScale.Y, blackPointScale.Z);

            Vector4 adaptedBlackPoint = Vector4.Transform(blackPointVector, Matrix4x4.Transpose(chadMatrix));
            blackPointOffsetTransform = new FunctionColorTransform(x => x + adaptedBlackPoint);
            blackPointCompensationTransform = IccBlackPoint.BuildBlackPointCompensationTransform(IccBlackPoint.GetNeutralBlackPoint(adaptedBlackPoint));
        }

        Matrix4x4 adaptedMatrix = Matrix4x4.Multiply(chadMatrix, Matrix4x4.Multiply(blackPointScaleMatrix, primariesMatrix));
        adaptedMatrix = Matrix4x4.Transpose(adaptedMatrix);

        MatrixColorTransform matrixTransform = new(adaptedMatrix);

        _toSrgbTransform = new ChainedColorTransform(trcTransform, matrixTransform, blackPointOffsetTransform, blackPointCompensationTransform, IccTransforms.XyzD50ToSrgbTransform);
        _absoluteToSrgbTransform = new ChainedColorTransform(trcTransform, matrixTransform, blackPointOffsetTransform, IccTransforms.XyzD50ToSrgbTransform);
    }

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <summary>
    /// Gets the transform taking this space's components to sRGB under the specified rendering intent.
    /// </summary>
    protected ChainedColorTransform GetToSrgbTransform(PdfRenderingIntent renderingIntent)
    {
        if (renderingIntent == PdfRenderingIntent.AbsoluteColorimetric)
        {
            return _absoluteToSrgbTransform;
        }

        return _toSrgbTransform;
    }

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
        => new(new ChainedColorTransform(GetToSrgbTransform(parameters.RenderingIntent), parameters.TransferFunction));
}
