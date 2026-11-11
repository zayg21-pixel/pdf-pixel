using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;
using System;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// A CIE-based color space defined by a white point, a black point, gamma, and a matrix to CIE XYZ:
/// CalGray or CalRGB.
/// </summary>
public abstract class PdfCalibratedColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly ChainedColorTransform _toXyzTransform;
    private readonly ChainedColorTransform _absoluteToXyzTransform;
    private readonly ChainedColorTransform _fromXyzTransform;
    private readonly ChainedColorTransform _absoluteFromXyzTransform;

    private protected PdfCalibratedColorSpaceConverter(float[] whitePoint, float[] blackPoint, float[] gamma, float[] matrix)
    {
        if (whitePoint == null)
        {
            throw new ArgumentNullException(nameof(whitePoint));
        }

        if (blackPoint == null)
        {
            throw new ArgumentNullException(nameof(blackPoint));
        }

        if (gamma == null)
        {
            throw new ArgumentNullException(nameof(gamma));
        }

        if (matrix == null)
        {
            throw new ArgumentNullException(nameof(matrix));
        }

        WhitePoint = whitePoint;
        BlackPoint = blackPoint;

        Vector4 whitePointVector = new(whitePoint[0], whitePoint[1], whitePoint[2], 1f);
        Vector4 blackPointVector = new(blackPoint[0], blackPoint[1], blackPoint[2], 0f);

        IccTrc[] gammaTrcs = [IccTrc.FromGamma(gamma[0]), IccTrc.FromGamma(gamma[1]), IccTrc.FromGamma(gamma[2])];
        PerChannelTrcTransform trcTransform = new(gammaTrcs);
        PerChannelTrcTransform inverseTrcTransform = new(gammaTrcs, inverse: true);

        Matrix4x4 chadMatrix = IccTransforms.BuildBradfordAdaptMatrix(whitePointVector, IccTransforms.D50WhitePoint);
        Matrix4x4 primariesMatrix = new(
            matrix[0],
            matrix[1],
            matrix[2],
            0f,
            matrix[3],
            matrix[4],
            matrix[5],
            0f,
            matrix[6],
            matrix[7],
            matrix[8],
            0f,
            0f,
            0f,
            0f,
            1f);
        primariesMatrix = Matrix4x4.Transpose(primariesMatrix); // this matches how PDF specifies the matrix

        Matrix4x4 blackPointScaleMatrix = Matrix4x4.Identity;
        FunctionColorTransform? blackPointOffsetTransform = null;
        FunctionColorTransform? inverseBlackPointOffsetTransform = null;
        IColorTransform? blackPointCompensationTransform = null;
        IColorTransform? inverseBlackPointCompensationTransform = null;

        // BlackPoint is the media black: the components decode into [BlackPoint, WhitePoint], then black point
        // compensation maps the neutral black at its lightness, at most L* 50, to zero; absolute colorimetry
        // skips the compensation.
        if (blackPointVector != Vector4.Zero)
        {
            Vector4 blackPointScale = (whitePointVector - blackPointVector) / whitePointVector;
            blackPointScaleMatrix = Matrix4x4.CreateScale(blackPointScale.X, blackPointScale.Y, blackPointScale.Z);

            Vector4 adaptedBlackPoint = Vector4.Transform(blackPointVector, Matrix4x4.Transpose(chadMatrix));
            Vector4 neutralBlackPoint = IccBlackPoint.GetNeutralBlackPoint(adaptedBlackPoint);
            blackPointOffsetTransform = new FunctionColorTransform(x => x + adaptedBlackPoint);
            inverseBlackPointOffsetTransform = new FunctionColorTransform(x => x - adaptedBlackPoint);
            blackPointCompensationTransform = IccBlackPoint.BuildBlackPointCompensationTransform(neutralBlackPoint, inverse: false);
            inverseBlackPointCompensationTransform = IccBlackPoint.BuildBlackPointCompensationTransform(neutralBlackPoint, inverse: true);
        }

        Matrix4x4 adaptedMatrix = Matrix4x4.Multiply(chadMatrix, Matrix4x4.Multiply(blackPointScaleMatrix, primariesMatrix));
        adaptedMatrix = Matrix4x4.Transpose(adaptedMatrix);

        MatrixColorTransform matrixTransform = new(adaptedMatrix);
        MatrixColorTransform inverseMatrixTransform = new(adaptedMatrix, inverse: true);

        _toXyzTransform = new ChainedColorTransform(trcTransform, matrixTransform, blackPointOffsetTransform, blackPointCompensationTransform);
        _absoluteToXyzTransform = new ChainedColorTransform(trcTransform, matrixTransform, blackPointOffsetTransform);
        _fromXyzTransform = new ChainedColorTransform(inverseBlackPointCompensationTransform, inverseBlackPointOffsetTransform, inverseMatrixTransform, inverseTrcTransform);
        _absoluteFromXyzTransform = new ChainedColorTransform(inverseBlackPointOffsetTransform, inverseMatrixTransform, inverseTrcTransform);
    }

    /// <summary>
    /// Gets the diffuse white point in CIE XYZ (/WhitePoint), or D65 when absent.
    /// </summary>
    public float[] WhitePoint { get; }

    /// <summary>
    /// Gets the diffuse black point in CIE XYZ (/BlackPoint), or [0 0 0] when absent.
    /// </summary>
    public float[] BlackPoint { get; }

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <summary>
    /// Gets the transform taking this space's components to CIE XYZ D50 under the specified rendering intent.
    /// </summary>
    private protected ChainedColorTransform GetToXyzTransform(PdfRenderingIntent renderingIntent)
    {
        if (renderingIntent == PdfRenderingIntent.AbsoluteColorimetric)
        {
            return _absoluteToXyzTransform;
        }

        return _toXyzTransform;
    }

    /// <summary>
    /// Gets the transform taking CIE XYZ D50 to this space's components under the specified rendering intent: the
    /// inverse of <see cref="GetToXyzTransform"/>.
    /// </summary>
    private protected ChainedColorTransform GetFromXyzTransform(PdfRenderingIntent renderingIntent)
    {
        if (renderingIntent == PdfRenderingIntent.AbsoluteColorimetric)
        {
            return _absoluteFromXyzTransform;
        }

        return _fromXyzTransform;
    }

    /// <summary>
    /// Gets the white point to use: the first three values of the dictionary's, or D65.
    /// </summary>
    private protected static float[] GetWhitePointOrDefault(float[]? whitePoint)
    {
        if (whitePoint?.Length >= 3)
        {
            return whitePoint.AsSpan(0, 3).ToArray();
        }

        Vector4 d65 = IccTransforms.D65WhitePoint;
        return new float[] { d65.X, d65.Y, d65.Z };
    }

    /// <summary>
    /// Gets the black point to use: the first three values of the dictionary's, or [0 0 0].
    /// </summary>
    private protected static float[] GetBlackPointOrDefault(float[]? blackPoint)
    {
        if (blackPoint?.Length >= 3)
        {
            return blackPoint.AsSpan(0, 3).ToArray();
        }

        return new float[] { 0f, 0f, 0f };
    }
}
