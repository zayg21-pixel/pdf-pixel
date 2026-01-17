using PdfRender.Color.Icc.Model;
using PdfRender.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfRender.Color.Icc;

/// <summary>
/// Provides ICC color space transformation utilities and standard color space transforms.
/// </summary>
internal static class IccTransforms
{
    private static readonly Matrix4x4 XyzD65ToRgbLinearMatrix = new Matrix4x4(
        3.2406255f,  -1.5372080f, -0.4986286f, 0f,
        -0.9689307f,  1.8757561f,  0.0415175f, 0f,
        0.0557101f,  -0.2040211f,  1.0569959f, 0f,
        0f,        0f,       0f,      1f);

    private static Matrix4x4 BradfordMatrix = new Matrix4x4(
        0.8951f, 0.2664f, -0.1614f, 0f,
        -0.7502f, 1.7135f, 0.0367f, 0f,
        0.0389f, -0.0685f, 1.0296f, 0f,
        0f, 0f, 0f, 1f);

    private static Matrix4x4 BradfordTransposedMatrix;
    private static Matrix4x4 BradfordInvertedMatrix;

    private static Matrix4x4 LabMatrixTransform = new Matrix4x4(
        // column 1 (fx):  X*(100/116), Y*(255/500), Z*0, W*(16/116 - 128/500)
        100f / 116f, 100f / 116f, 100f / 116f, 0f,
        // column 2 (fy):  X*(100/116), Y*0,        Z*0,  W*(16/116)
        255f / 500f, 0f, 0f, 0f,
        // column 3 (fz):  X*(100/116), Y*0,        Z*(-255/200), W*(16/116 + 128/200)
        0f, 0f, -255f / 200f, 0f,
        // column 4 (w):   X*0,        Y*0,        Z*0,  W*1
        16f / 116f - 128f / 500f, 16f / 116f, 16f / 116f + 128f / 200f, 1f);

    private static IColorTransform SrgbLinearToSrgbTransform = BuildSrgbLinearToSrgbTransform();

    static IccTransforms()
    {
        BradfordTransposedMatrix = Matrix4x4.Transpose(BradfordMatrix);
        Matrix4x4.Invert(BradfordMatrix, out BradfordInvertedMatrix);
        XyzD50ToSrgbTransform = BuildXyzD50ToSrgbTransform();
        LabD50ToXyzTransform = BuildLabD50ToXyzTransform();
    }

    /// <summary>
    /// Gets the color transform that converts from CIE XYZ (D50) to sRGB.
    /// </summary>
    public static IColorTransform XyzD50ToSrgbTransform { get; }

    /// <summary>
    /// Gets the color transform that converts from CIE Lab (D50) to CIE XYZ.
    /// </summary>
    public static IColorTransform LabD50ToXyzTransform { get; }

    /// <summary>
    /// Gets the D50 white point as a Vector4.
    /// </summary>
    public static Vector4 D50WhitePoint { get; } = new Vector4(0.9642f, 1.0000f, 0.8249f, 1.0f);

    /// <summary>
    /// Gets the D65 white point as a Vector4.
    /// </summary>
    public static Vector4 D65WhitePoint { get; } = new Vector4(0.9505f, 1.0000f, 1.0890f, 1.0f);

    /// <summary>
    /// Builds a color transform from CIE XYZ (D50) to sRGB.
    /// </summary>
    /// <returns>An <see cref="IColorTransform"/> that converts XYZ D50 to sRGB.</returns>
    public static IColorTransform BuildXyzD50ToSrgbTransform()
    {
        var d50ToD65 = BuildBradfordAdaptMatrix(D50WhitePoint, D65WhitePoint);
        return BuildXyzToSrgbTransform(d50ToD65);
    }

    /// <summary>
    /// Builds a color transform from CIE XYZ (with the specified source white point) to sRGB.
    /// </summary>
    /// <param name="sourceWhite">The source white point.</param>
    /// <returns>An <see cref="IColorTransform"/> that converts XYZ to sRGB.</returns>
    public static IColorTransform BuildXyzToSrgbTransform(Vector4 sourceWhite)
    {
        var adaptationMatrix4x4 = BuildBradfordAdaptMatrix(sourceWhite, D65WhitePoint);
        return BuildXyzToSrgbTransform(adaptationMatrix4x4);
    }

    /// <summary>
    /// Builds a color transform from CIE XYZ to sRGB using the specified adaptation matrix.
    /// </summary>
    /// <param name="adaptationMatrix">The chromatic adaptation matrix.</param>
    /// <returns>An <see cref="IColorTransform"/> that converts XYZ to sRGB.</returns>
    public static IColorTransform BuildXyzToSrgbTransform(Matrix4x4 adaptationMatrix)
    {
        var D50ToSrgbLinearMatrix4x4 = Matrix4x4.Multiply(XyzD65ToRgbLinearMatrix, adaptationMatrix);
        var transposed = Matrix4x4.Transpose(D50ToSrgbLinearMatrix4x4);

        return new ChainedColorTransform(new MatrixColorTransform(transposed), SrgbLinearToSrgbTransform);
    }

    private static IColorTransform BuildSrgbLinearToSrgbTransform()
    {
        // Build ICC parametric type 4 TRC matching sRGB companding.
        // For x < d: y = c * x + f
        // For x >= d: y = (a * x + b)^g + e
        // sRGB forward companding: if x <= 0.0031308 -> 12.92 * x; else 1.055 * x^(1/2.4) - 0.055
        // In type 4, the 1.055 factor is inside the power: a^g == 1.055, so a == 1.055^(2.4) ≈ 1.13712.
        float g = 1.0f / 2.4f;
        float a = 1.13712f; // 1.055^(2.4)
        float b = 0.0f;
        float c = 12.92f;
        float d = 0.0031308f;
        float e = -0.055f;
        float f = 0.0f;

        float[] parameters = [g, a, b, c, d, e, f];

        var srgbParametric = IccTrc.FromParametric(IccTrcParametricType.PowerWithLinearSegmentAndOffset, parameters);

        return new PerChannelTrcTransform(srgbParametric, srgbParametric, srgbParametric);
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
        var r3 = Unsafe.As<Vector4, Vector3>(ref r);

        Matrix4x4 diagonalScaling = Matrix4x4.CreateScale(r3);

        // Adaptation matrix: Minv * D * M
        Matrix4x4 DM = Matrix4x4.Multiply(diagonalScaling, BradfordMatrix);
        Matrix4x4 result = Matrix4x4.Multiply(BradfordInvertedMatrix, DM);
        return result;
    }

    /// <summary>
    /// Builds a color transform from CIE Lab (D50) to CIE XYZ.
    /// </summary>
    /// <returns>An <see cref="IColorTransform"/> that converts Lab D50 to XYZ.</returns>
    public static IColorTransform BuildLabD50ToXyzTransform()
    {
        return BuildLabToXyzTransform(D50WhitePoint);
    }

    /// <summary>
    /// Builds a color transform from CIE Lab to CIE XYZ using the specified white point.
    /// </summary>
    /// <param name="whitePoint">The reference white point.</param>
    /// <returns>An <see cref="IColorTransform"/> that converts Lab to XYZ.</returns>
    public static IColorTransform BuildLabToXyzTransform(Vector4 whitePoint)
    {
        var matrixTransform = new MatrixColorTransform(LabMatrixTransform);

        // Cube function to apply f^3, specification also uses conditional logic which we handle
        // but it creates minimal (<= 0.03) difference in results.
        // Original function:
        //  if f^3 >= 0.008856f ? f^3 : (f - 0.137931034) * 0.12841855;
        var cubeWhitePointTransform = new FunctionColorTransform(x => x = x * x * x * whitePoint);


        return new ChainedColorTransform(matrixTransform, cubeWhitePointTransform);
    }
}
