using PdfPixel.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc;

/// <summary>
/// Provides ICC color space transformation utilities and standard color space transforms.
/// </summary>
public static class IccTransforms
{
    private const float LabBreakpoint = 6f / 29f;
    private const float LabLinearSlope = 108f / 841f;
    private const float LabLinearOffset = (4f / 29f * LabLinearSlope) + (LabBreakpoint * LabBreakpoint * LabBreakpoint);

    private static readonly Matrix4x4 XyzD65ToRgbLinearMatrix = new(
        3.2406255f,
        -1.5372080f,
        -0.4986286f,
        0f,
        -0.9689307f,
        1.8757561f,
        0.0415175f,
        0f,
        0.0557101f,
        -0.2040211f,
        1.0569959f,
        0f,
        0f,
        0f,
        0f,
        1f);

    private static Matrix4x4 BradfordMatrix = new(
        0.8951f,
        0.2664f,
        -0.1614f,
        0f,
        -0.7502f,
        1.7135f,
        0.0367f,
        0f,
        0.0389f,
        -0.0685f,
        1.0296f,
        0f,
        0f,
        0f,
        0f,
        1f);

    private static Matrix4x4 BradfordTransposedMatrix;
    private static Matrix4x4 BradfordInvertedMatrix;

    private static Matrix4x4 LabMatrixTransform = new(
        // column 1 (fx):  X*(100/116), Y*(255/500), Z*0, W*(16/116 - 128/500)
        100f / 116f,
        100f / 116f,
        100f / 116f,
        0f,
        // column 2 (fy):  X*(100/116), Y*0,        Z*0,  W*(16/116)
        255f / 500f,
        0f,
        0f,
        0f,
        // column 3 (fz):  X*(100/116), Y*0,        Z*(-255/200), W*(16/116 + 128/200)
        0f,
        0f,
        -255f / 200f,
        0f,
        // column 4 (w):   X*0,        Y*0,        Z*0,  W*1
        (16f / 116f) - (128f / 500f),
        16f / 116f,
        (16f / 116f) + (128f / 200f),
        1f);

    private static readonly PerChannelTrcTransform SrgbLinearToSrgbTransform = BuildSrgbLinearToSrgbTransform();
    private static readonly PerChannelTrcTransform SrgbToSrgbLinearTransform = BuildSrgbToSrgbLinearTransform();

    static IccTransforms()
    {
        BradfordTransposedMatrix = Matrix4x4.Transpose(BradfordMatrix);
        Matrix4x4.Invert(BradfordMatrix, out BradfordInvertedMatrix);
        XyzD50ToSrgbTransform = BuildXyzD50ToSrgbTransform();
        SrgbToXyzD50Transform = BuildSrgbToXyzD50Transform();
        XyzD50ToSrgbGrayTransform = BuildXyzD50ToSrgbGrayTransform();
        LabD50ToXyzTransform = BuildLabD50ToXyzTransform();
        XyzD50ToLabTransform = BuildXyzToLabTransform(D50WhitePoint);
    }

    /// <summary>
    /// Gets the color transform that converts from CIE XYZ (D50) to sRGB.
    /// </summary>
    public static ChainedColorTransform XyzD50ToSrgbTransform { get; }

    /// <summary>
    /// Gets the color transform that converts from sRGB to CIE XYZ (D50).
    /// </summary>
    public static ChainedColorTransform SrgbToXyzD50Transform { get; }

    /// <summary>
    /// Gets the color transform that converts from CIE XYZ (D50) to the sRGB gray of the same luminance, in the
    /// first component.
    /// </summary>
    public static ChainedColorTransform XyzD50ToSrgbGrayTransform { get; }

    /// <summary>
    /// Gets the color transform that converts from CIE Lab (D50) to CIE XYZ.
    /// </summary>
    public static ChainedColorTransform LabD50ToXyzTransform { get; }

    /// <summary>
    /// Gets the color transform that converts from CIE XYZ to CIE Lab (D50), encoded as
    /// <see cref="LabD50ToXyzTransform"/> takes it.
    /// </summary>
    public static ChainedColorTransform XyzD50ToLabTransform { get; }

    /// <summary>
    /// Gets the D50 white point as a Vector4.
    /// </summary>
    public static Vector4 D50WhitePoint { get; } = new(0.9642f, 1.0000f, 0.8249f, 1.0f);

    /// <summary>
    /// Gets the D65 white point as a Vector4.
    /// </summary>
    public static Vector4 D65WhitePoint { get; } = new(0.9505f, 1.0000f, 1.0890f, 1.0f);

    /// <summary>
    /// Builds a color transform from CIE XYZ (D50) to sRGB.
    /// </summary>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts XYZ D50 to sRGB.</returns>
    public static ChainedColorTransform BuildXyzD50ToSrgbTransform()
    {
        Matrix4x4 d50ToD65 = BuildBradfordAdaptMatrix(D50WhitePoint, D65WhitePoint);
        return BuildXyzToSrgbTransform(d50ToD65);
    }

    /// <summary>
    /// Builds a color transform from CIE XYZ (with the specified source white point) to sRGB.
    /// </summary>
    /// <param name="sourceWhite">The source white point.</param>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts XYZ to sRGB.</returns>
    public static ChainedColorTransform BuildXyzToSrgbTransform(Vector4 sourceWhite)
    {
        Matrix4x4 adaptationMatrix4x4 = BuildBradfordAdaptMatrix(sourceWhite, D65WhitePoint);
        return BuildXyzToSrgbTransform(adaptationMatrix4x4);
    }

    /// <summary>
    /// Builds a color transform from CIE XYZ to sRGB using the specified adaptation matrix.
    /// </summary>
    /// <param name="adaptationMatrix">The chromatic adaptation matrix.</param>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts XYZ to sRGB.</returns>
    public static ChainedColorTransform BuildXyzToSrgbTransform(Matrix4x4 adaptationMatrix)
    {
        Matrix4x4 d50ToSrgbLinearMatrix4x4 = Matrix4x4.Multiply(XyzD65ToRgbLinearMatrix, adaptationMatrix);
        Matrix4x4 transposed = Matrix4x4.Transpose(d50ToSrgbLinearMatrix4x4);

        return new ChainedColorTransform(new MatrixColorTransform(transposed), SrgbLinearToSrgbTransform);
    }

    /// <summary>
    /// Builds a color transform from sRGB to CIE XYZ (D50).
    /// </summary>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts sRGB to XYZ D50.</returns>
    public static ChainedColorTransform BuildSrgbToXyzD50Transform()
    {
        Matrix4x4 d50ToD65 = BuildBradfordAdaptMatrix(D50WhitePoint, D65WhitePoint);
        Matrix4x4 d50ToSrgbLinearMatrix4x4 = Matrix4x4.Multiply(XyzD65ToRgbLinearMatrix, d50ToD65);
        Matrix4x4.Invert(d50ToSrgbLinearMatrix4x4, out Matrix4x4 srgbLinearToD50Matrix4x4);
        Matrix4x4 transposed = Matrix4x4.Transpose(srgbLinearToD50Matrix4x4);

        return new ChainedColorTransform(SrgbToSrgbLinearTransform, new MatrixColorTransform(transposed));
    }

    /// <summary>
    /// Builds a color transform from CIE XYZ (D50) to the sRGB gray of the same luminance, in the first component.
    /// </summary>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts XYZ D50 to sRGB gray.</returns>
    public static ChainedColorTransform BuildXyzD50ToSrgbGrayTransform()
    {
        FunctionColorTransform luminanceTransform = new(color => new Vector4(color.Y, color.Y, color.Y, color.W));

        return new ChainedColorTransform(luminanceTransform, SrgbLinearToSrgbTransform);
    }

    private static PerChannelTrcTransform BuildSrgbToSrgbLinearTransform()
    {
        // sRGB decoding as an ICC parametric type 4 TRC:
        // For x < d: y = c * x + f
        // For x >= d: y = (a * x + b)^g + e
        // if x < 0.04045 -> x / 12.92; else ((x + 0.055) / 1.055)^2.4
        const float g = 2.4f;
        const float a = 1f / 1.055f;
        const float b = 0.055f / 1.055f;
        const float c = 1f / 12.92f;
        const float d = 0.04045f;
        const float e = 0.0f;
        const float f = 0.0f;

        float[] parameters = [g, a, b, c, d, e, f];

        IccTrc srgbParametric = IccTrc.FromParametric(IccTrcParametricType.PowerWithLinearSegmentAndOffset, parameters);

        return new PerChannelTrcTransform([srgbParametric, srgbParametric, srgbParametric]);
    }

    private static PerChannelTrcTransform BuildSrgbLinearToSrgbTransform()
    {
        // Build ICC parametric type 4 TRC matching sRGB companding.
        // For x < d: y = c * x + f
        // For x >= d: y = (a * x + b)^g + e
        // sRGB forward companding: if x <= 0.0031308 -> 12.92 * x; else 1.055 * x^(1/2.4) - 0.055
        // In type 4, the 1.055 factor is inside the power: a^g == 1.055, so a == 1.055^(2.4) ≈ 1.13712.
        const float g = 1.0f / 2.4f;
        const float a = 1.13712f; // 1.055^(2.4)
        const float b = 0.0f;
        const float c = 12.92f;
        const float d = 0.0031308f;
        const float e = -0.055f;
        const float f = 0.0f;

        float[] parameters = [g, a, b, c, d, e, f];

        IccTrc srgbParametric = IccTrc.FromParametric(IccTrcParametricType.PowerWithLinearSegmentAndOffset, parameters);

        return new PerChannelTrcTransform([srgbParametric, srgbParametric, srgbParametric]);
    }

    /// <summary>
    /// Builds a chromatic adaptation matrix using the Bradford method.
    /// </summary>
    /// <param name="sourceWhite">The source white point.</param>
    /// <param name="destinationWhite">The destination white point.</param>
    /// <returns>A <see cref="Matrix4x4"/> representing the adaptation matrix.</returns>
    public static Matrix4x4 BuildBradfordAdaptMatrix(Vector4 sourceWhite, Vector4 destinationWhite)
    {
        Vector4 s = Vector4.Transform(sourceWhite, BradfordTransposedMatrix);
        Vector4 d = Vector4.Transform(destinationWhite, BradfordTransposedMatrix);

        Vector4 r = d / s;
        Vector3 r3 = Unsafe.As<Vector4, Vector3>(ref r);

        Matrix4x4 diagonalScaling = Matrix4x4.CreateScale(r3);

        // Adaptation matrix: Minv * D * M
        Matrix4x4 DM = Matrix4x4.Multiply(diagonalScaling, BradfordMatrix);
        Matrix4x4 result = Matrix4x4.Multiply(BradfordInvertedMatrix, DM);
        return result;
    }

    /// <summary>
    /// Builds a color transform from CIE Lab (D50) to CIE XYZ.
    /// </summary>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts Lab D50 to XYZ.</returns>
    public static ChainedColorTransform BuildLabD50ToXyzTransform() => BuildLabToXyzTransform(D50WhitePoint);

    /// <summary>
    /// Builds a color transform from CIE Lab to CIE XYZ using the specified white point.
    /// </summary>
    /// <param name="whitePoint">The reference white point.</param>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts Lab to XYZ.</returns>
    public static ChainedColorTransform BuildLabToXyzTransform(Vector4 whitePoint)
    {
        MatrixColorTransform matrixTransform = new(LabMatrixTransform);

        // For f >= 6/29: g(f) = f^3
        // For f < 6/29: g(f) = 108/841 * (f - 4/29)
        // Both at once: g(f) = max(f, 6/29)^3 + min(f, 6/29) * 108/841 - (4/29 * 108/841 + (6/29)^3)
        Vector4 breakpoint = new(LabBreakpoint);
        Vector4 linearOffset = new(LabLinearOffset);
        FunctionColorTransform cubeWhitePointTransform = new(x =>
        {
            Vector4 cubed = Vector4.Max(x, breakpoint);
            Vector4 linear = Vector4.Min(x, breakpoint);
            return ((cubed * cubed * cubed) + (linear * LabLinearSlope) - linearOffset) * whitePoint;
        });

        return new ChainedColorTransform(matrixTransform, cubeWhitePointTransform);
    }

    /// <summary>
    /// Builds a color transform from CIE XYZ to CIE Lab using the specified white point, encoded as
    /// <see cref="BuildLabToXyzTransform"/> takes it: L* / 100, (a* + 128) / 255 and (b* + 128) / 255.
    /// </summary>
    /// <param name="whitePoint">The reference white point.</param>
    /// <returns>A <see cref="ChainedColorTransform"/> that converts XYZ to Lab.</returns>
    public static ChainedColorTransform BuildXyzToLabTransform(Vector4 whitePoint)
    {
        FunctionColorTransform labTransform = new(x =>
        {
            Vector4 relative = x / whitePoint;
            float fx = GetLabFunction(relative.X);
            float fy = GetLabFunction(relative.Y);
            float fz = GetLabFunction(relative.Z);

            return new Vector4(
                ((116f * fy) - 16f) / 100f,
                ((500f * (fx - fy)) + 128f) / 255f,
                ((200f * (fy - fz)) + 128f) / 255f,
                x.W);
        });

        return new ChainedColorTransform(labTransform);
    }

    /// <summary>
    /// Returns f(t) of the CIE Lab definition: the cube root of <paramref name="value"/> above (6/29)³, its linear
    /// continuation below.
    /// </summary>
    private static float GetLabFunction(float value)
    {
        if (value > LabBreakpoint * LabBreakpoint * LabBreakpoint)
        {
            return (float)Math.Pow(value, 1.0 / 3.0);
        }
        else
        {
            return (value / (3f * LabBreakpoint * LabBreakpoint)) + (4f / 29f);
        }
    }
}
