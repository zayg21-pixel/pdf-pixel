using PdfReader.Color.ColorSpace;
using PdfReader.Color.Icc.Model;
using System;
using System.Numerics;

namespace PdfReader.Color.Icc.Utilities;

/// <summary>
/// Helper utilities and constants used by ICC profile evaluation and color conversion logic.
/// Provides white point constants, precomputed transformation matrices, lookup tables for
/// sRGB companding and cube roots, and convenience functions for TRC expansion, black point
/// compensation metrics, chromatic adaptation, and rendering-intent-based LUT selection.
/// </summary>
internal static class IccProfileHelpers
{
    /// <summary>
    /// D50 reference white (XYZ) used as PCS white point in ICC workflows.
    /// </summary>
    public static readonly Vector3 D50WhitePoint = new Vector3(0.9642f, 1.0000f, 0.8249f);

    /// <summary>
    /// Element-wise inverse of <see cref="D50WhitePoint"/> for fast XYZ -> Lab conversions.
    /// </summary>
    public static readonly Vector3 D50WhitePointInverse = new Vector3(1f / 0.9642f, 1f / 1.0f, 1f / 0.8249f);

    /// <summary>
    /// Bradford adaptation matrix D50 -> D65.
    /// </summary>
    public static readonly float[,] D50ToD65;

    /// <summary>
    /// D65-referenced XYZ -> linear sRGB matrix (standard sRGB definition).
    /// </summary>
    public static readonly float[,] XyzD65ToRgbLinear;

    /// <summary>
    /// Combined matrix: XYZ(D50) -> linear sRGB (D50 first adapted to D65 then XYZ->RGB).
    /// </summary>
    public static readonly float[,] D50ToSrgbLinear;

    /// <summary>
    /// Row 0 of <see cref="D50ToSrgbLinear"/> as a vector for dot-product optimization.
    /// </summary>
    public static readonly Vector3 D50ToSrgbRow0;

    /// <summary>
    /// Row 1 of <see cref="D50ToSrgbLinear"/> as a vector for dot-product optimization.
    /// </summary>
    public static readonly Vector3 D50ToSrgbRow1;

    /// <summary>
    /// Row 2 of <see cref="D50ToSrgbLinear"/> as a vector for dot-product optimization.
    /// </summary>
    public static readonly Vector3 D50ToSrgbRow2;

    /// <summary>
    /// Standard size used for high-resolution lookup table generation (e.g., TRC, companding, cube-root).
    /// </summary>
    public const int LutTableSize = 2048;

    /// <summary>
    /// Size of precomputed sRGB companding LUT (same as <see cref="LutTableSize"/>).
    /// </summary>
    public const int SrgbCompLutSize = LutTableSize;

    /// <summary>
    /// Precomputed sRGB companding lookup table (nearest sampling). Length = SrgbCompLutSize + 1.
    /// </summary>
    public static readonly float[] SrgbCompLut = new float[SrgbCompLutSize + 1];

    /// <summary>
    /// Cube-root LUT size (used for Lab conversions) – same resolution as general LUT size.
    /// </summary>
    public const int CbrtLutSize = LutTableSize;

    /// <summary>
    /// Maximum input value represented in <see cref="CbrtLut"/>.
    /// </summary>
    public const float CbrtLutMax = 4f;

    /// <summary>
    /// 1 / <see cref="CbrtLutMax"/> – factor used for normalization in cube-root lookup.
    /// </summary>
    public const float CbrtLutMaxInv = 1f / CbrtLutMax;

    /// <summary>
    /// Scale factor mapping input domain (0..CbrtLutMax) to LUT indices.
    /// </summary>
    public const float CbrtScale = CbrtLutSize / CbrtLutMax;

    /// <summary>
    /// Cube-root lookup table of size CbrtLutSize + 1 storing t^(1/3) for performance.
    /// </summary>
    public static readonly float[] CbrtLut = new float[CbrtLutSize + 1];

    /// <summary>
    /// Lab epsilon ( (6/29)^3 ) threshold separating linear and cubic regions.
    /// </summary>
    public const float LabEpsilon = 0.008856f;

    /// <summary>
    /// Linear slope constant for Lab f(t) below epsilon ( 1 / (3*(6/29)^2) ).
    /// </summary>
    public const float LabLinearA = 7.787037f;

    /// <summary>
    /// Linear intercept constant for Lab f(t) below epsilon ( 16/116 ).
    /// </summary>
    public const float LabLinearB = 16f / 116f;

    /// <summary>
    /// Default LUT size used when expanding TRCs to lookup tables.
    /// </summary>
    public const int TrcLutSize = LutTableSize;

    static IccProfileHelpers()
    {
        D50ToD65 = new float[,]
        {
            { 0.9555766f, -0.0230393f,  0.0631636f },
            { -0.0282895f, 1.0099416f,  0.0210077f },
            { 0.0122982f, -0.0204830f, 1.3299098f }
        };

        XyzD65ToRgbLinear = new float[,]
        {
            { 3.2406f,  -1.5372f, -0.4986f },
            { -0.9689f,  1.8758f,  0.0415f },
            { 0.0557f,  -0.2040f,  1.0570f }
        };

        D50ToSrgbLinear = Multiply3x3(XyzD65ToRgbLinear, D50ToD65);
        D50ToSrgbRow0 = new Vector3(D50ToSrgbLinear[0, 0], D50ToSrgbLinear[0, 1], D50ToSrgbLinear[0, 2]);
        D50ToSrgbRow1 = new Vector3(D50ToSrgbLinear[1, 0], D50ToSrgbLinear[1, 1], D50ToSrgbLinear[1, 2]);
        D50ToSrgbRow2 = new Vector3(D50ToSrgbLinear[2, 0], D50ToSrgbLinear[2, 1], D50ToSrgbLinear[2, 2]);

        for (int index = 0; index <= SrgbCompLutSize; index++)
        {
            float x = (float)index / SrgbCompLutSize;
            SrgbCompLut[index] = ComputeSrgbCompandScalar(x);
        }

        for (int index = 0; index <= CbrtLutSize; index++)
        {
            float t = index * CbrtScale;
            CbrtLut[index] = t <= 0f ? 0f : MathF.Pow(t, 1f / 3f);
        }
    }

    /// <summary>
    /// Multiply a device RGB -> PCS matrix by the profile chromatic adaptation matrix if present and return row vectors.
    /// </summary>
    public static (Vector3 Row0, Vector3 Row1, Vector3 Row2) AdaptRgbMatrixToPcsRows(IccProfile profile, float[,] sourceMatrix)
    {
        if (sourceMatrix == null)
        {
            return (default, default, default);
        }

        float[,] working = profile?.ChromaticAdaptation != null ? Multiply3x3(profile.ChromaticAdaptation, sourceMatrix) : sourceMatrix;
        Vector3 row0 = new Vector3(working[0, 0], working[0, 1], working[0, 2]);
        Vector3 row1 = new Vector3(working[1, 0], working[1, 1], working[1, 2]);
        Vector3 row2 = new Vector3(working[2, 0], working[2, 1], working[2, 2]);
        return (row0, row1, row2);
    }

    /// <summary>
    /// Expand a tone reproduction curve to a uniformly sampled lookup table (size + 1 entries).
    /// </summary>
    public static float[] IccTrcToLut(IccTrc trc, int size = TrcLutSize)
    {
        if (trc == null)
        {
            return null;
        }

        int sampleCount = size > 1 ? size : TrcLutSize;
        float[] lut = new float[sampleCount + 1];
        for (int index = 0; index <= sampleCount; index++)
        {
            float x = index / (float)sampleCount;
            lut[index] = IccTrcEvaluator.EvaluateTrc(trc, x);
        }
        return lut;
    }

    /// <summary>
    /// Derive source black point L* (0..100) from profile blackPoint tag (if present) clamped to usable range.
    /// Returns 0 when no usable black point is available.
    /// </summary>
    public static float GetSourceBlackLstar(IccProfile profile)
    {
        try
        {
            if (profile?.BlackPoint != null)
            {
                float X = profile.BlackPoint.Value.X;
                float Y = profile.BlackPoint.Value.Y;
                float Z = profile.BlackPoint.Value.Z;
                Vector3 xyz = new Vector3(X, Y, Z);
                Vector3 lab = ColorMath.XyzD50ToLab(in xyz);
                float L = lab.X;
                if (L > 0f && L < 50f)
                {
                    return L;
                }
            }
        }
        catch
        {
            // Safe to ignore – fall back to L*=0 indicates no compensation.
        }
        return 0f;
    }

    /// <summary>
    /// Compute scale factor used in black point compensation mapping (100/(100 - Lb)). Returns 1 if invalid.
    /// </summary>
    public static float GetBlackLstarScale(float sourceBlackL)
    {
        return sourceBlackL > 0f && sourceBlackL < 100f ? 100f / (100f - sourceBlackL) : 1f;
    }

    /// <summary>
    /// Multiply two 3x3 matrices (A * B).
    /// </summary>
    public static float[,] Multiply3x3(float[,] A, float[,] B)
    {
        if (A == null || B == null)
        {
            return B ?? A;
        }

        float[,] result = new float[3, 3];
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                result[rowIndex, columnIndex] = A[rowIndex, 0] * B[0, columnIndex] + A[rowIndex, 1] * B[1, columnIndex] + A[rowIndex, 2] * B[2, columnIndex];
            }
        }
        return result;
    }

    /// <summary>
    /// Multiply a 3x3 matrix by a vector (x,y,z).
    /// </summary>
    public static Vector3 Multiply3x3(float[,] matrix, float x, float y, float z)
    {
        if (matrix == null || matrix.Length < 9)
        {
            return new Vector3(x, y, z);
        }
        float rx = matrix[0, 0] * x + matrix[0, 1] * y + matrix[0, 2] * z;
        float ry = matrix[1, 0] * x + matrix[1, 1] * y + matrix[1, 2] * z;
        float rz = matrix[2, 0] * x + matrix[2, 1] * y + matrix[2, 2] * z;
        return new Vector3(rx, ry, rz);
    }

    /// <summary>
    /// Compute sRGB forward companding scalar for a linear component.
    /// </summary>
    private static float ComputeSrgbCompandScalar(float componentLinear)
    {
        if (componentLinear <= 0f)
        {
            return 0f;
        }
        if (componentLinear <= 0.0031308f)
        {
            return 12.92f * componentLinear;
        }
        return 1.055f * MathF.Pow(componentLinear, 1.0f / 2.4f) - 0.055f;
    }

    /// <summary>
    /// Create a Bradford adaptation matrix from source XYZ white to destination XYZ white.
    /// </summary>
    public static float[,] CreateBradfordAdaptMatrix(float xs, float ys, float zs, float xd, float yd, float zd)
    {
        float[,] M = new float[,]
        {
            { 0.8951f,  0.2664f, -0.1614f },
            { -0.7502f, 1.7135f,  0.0367f },
            { 0.0389f, -0.0685f,  1.0296f }
        };
        float[,] Minv = new float[,]
        {
            { 0.9869929f, -0.1470543f, 0.1599627f },
            { 0.4323053f,  0.5183603f, 0.0492912f },
            { -0.0085287f, 0.0400428f, 0.9684867f }
        };

        float sx = M[0, 0] * xs + M[0, 1] * ys + M[0, 2] * zs;
        float sy = M[1, 0] * xs + M[1, 1] * ys + M[1, 2] * zs;
        float sz = M[2, 0] * xs + M[2, 1] * ys + M[2, 2] * zs;
        float dx = M[0, 0] * xd + M[0, 1] * yd + M[0, 2] * zd;
        float dy = M[1, 0] * xd + M[1, 1] * yd + M[1, 2] * zd;
        float dz = M[2, 0] * xd + M[2, 1] * yd + M[2, 2] * zd;

        float rx = dx / (sx == 0f ? 1e-6f : sx);
        float ry = dy / (sy == 0f ? 1e-6f : sy);
        float rz = dz / (sz == 0f ? 1e-6f : sz);

        float[,] D = new float[,]
        {
            { rx, 0f, 0f },
            { 0f, ry, 0f },
            { 0f, 0f, rz }
        };

        float[,] DM = Multiply3x3(D, M);
        return Multiply3x3(Minv, DM);
    }

    /// <summary>
    /// Select appropriate parsed A2B LUT pipeline by explicit PDF rendering intent with ordered fallback.
    /// Header rendering intent is advisory and ignored here.
    /// </summary>
    public static IccLutPipeline GetA2BLutByIntent(IccProfile profile, PdfRenderingIntent intent)
    {
        if (profile == null)
        {
            return null;
        }

        switch (intent)
        {
            case PdfRenderingIntent.Perceptual:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
            case PdfRenderingIntent.RelativeColorimetric:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;
            case PdfRenderingIntent.Saturation:
                return profile.A2BLut2 ?? profile.A2BLut0 ?? profile.A2BLut1;
            case PdfRenderingIntent.AbsoluteColorimetric:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;
            default:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
        }
    }

    /// <summary>
    /// Overload that selects a parsed A2B LUT pipeline using the rendering intent stored in the profile header.
    /// Caller-specified intent should be preferred when available; this method is a convenience fallback.
    /// Header intent values: 0=Perceptual, 1=Relative Colorimetric, 2=Saturation, 3=Absolute Colorimetric.
    /// </summary>
    public static IccLutPipeline GetA2BLutByIntent(IccProfile profile)
    {
        if (profile == null)
        {
            return null;
        }

        uint intentValue = profile.Header != null ? profile.Header.RenderingIntent : 0u;
        switch (intentValue)
        {
            case 0u:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
            case 1u:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;
            case 2u:
                return profile.A2BLut2 ?? profile.A2BLut0 ?? profile.A2BLut1;
            case 3u:
                return profile.A2BLut1 ?? profile.A2BLut0 ?? profile.A2BLut2;
            default:
                return profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;
        }
    }
}
