using System;
using System.Numerics;

namespace PdfReader.Icc
{
    internal static class IccProfileHelpers
    {
        // D50 white point (PCS)
        public static readonly Vector3 D50WhitePoint = new Vector3(0.9642f, 1.0000f, 0.8249f);

        public static readonly Vector3 D50WhitePointInverse = new Vector3(1f / 0.9642f, 1f / 1.0f, 1f / 0.8249f);

        // XYZ(D65) -> sRGB linear and Bradford D50->D65, and combined D50->sRGB rows
        public static readonly float[,] D50ToD65;
        public static readonly float[,] XyzD65ToRgbLinear;
        public static readonly float[,] D50ToSrgbLinear;
        public static readonly Vector3 D50ToSrgbRow0;
        public static readonly Vector3 D50ToSrgbRow1;
        public static readonly Vector3 D50ToSrgbRow2;

        public const int LutTableSize = 8192;

        // sRGB companding LUT
        public const int SrgbCompLutSize = LutTableSize;
        public static readonly float[] SrgbCompLut = new float[SrgbCompLutSize + 1];

        // Cube-root LUT and constants for Lab conversions
        public const int CbrtLutSize = LutTableSize;
        public const float CbrtLutMax = 4f;
        public const float CbrtLutMaxInv = 1f / CbrtLutMax;
        public const float CbrtScale = CbrtLutSize / CbrtLutMax;
        public static readonly float[] CbrtLut = new float[CbrtLutSize + 1];

        public const float LabEpsilon = 0.008856f;   // (6/29)^3
        public const float LabLinearA = 7.787037f;   // 1/(3*(6/29)^2)
        public const float LabLinearB = 16f / 116f;  // 4/29

        // Default LUT size for TRC precomputation
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

            for (int i = 0; i <= SrgbCompLutSize; i++)
            {
                float x = (float)i / SrgbCompLutSize;
                SrgbCompLut[i] = ComputeSrgbCompandScalar(x);
            }

            for (int i = 0; i <= CbrtLutSize; i++)
            {
                float t = (float)i * CbrtScale;
                CbrtLut[i] = (t <= 0f) ? 0f : MathF.Pow(t, 1f / 3f);
            }
        }

        public static (Vector3 Row0, Vector3 Row1, Vector3 Row2) AdaptRgbMatrixToPcsRows(IccProfile profile, float[,] srcMatrix)
        {
            if (srcMatrix == null)
            {
                return (default, default, default);
            }

            var chad = profile?.ChromaticAdaptation;
            var m = (chad != null) ? Multiply3x3(chad, srcMatrix) : srcMatrix;
            var r0 = new Vector3(m[0, 0], m[0, 1], m[0, 2]);
            var r1 = new Vector3(m[1, 0], m[1, 1], m[1, 2]);
            var r2 = new Vector3(m[2, 0], m[2, 1], m[2, 2]);
            return (r0, r1, r2);
        }

        public static float[] IccTrcToLut(IccTrc c, int size = TrcLutSize)
        {
            if (c == null)
            {
                return null;
            }

            int n = size > 1 ? size : TrcLutSize;
            var lut = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float x = i / (float)n;
                lut[i] = IccTrcEvaluator.EvaluateTrc(c, x);
            }

            return lut;
        }

        public static float GetSourceBlackLstar(IccProfile p)
        {
            try
            {
                if (p?.BlackPoint != null)
                {
                    float X = p.BlackPoint.Value.X;
                    float Y = p.BlackPoint.Value.Y;
                    float Z = p.BlackPoint.Value.Z;
                    var xyz = new Vector3(X, Y, Z);
                    var lab = ColorMath.XyzD50ToLab(in xyz);
                    float L = lab.X;
                    if (L > 0f && L < 50f)
                    {
                        return L;
                    }
                }
            }
            catch
            {
                // ignore and fallback to 0
            }

            return 0f;
        }

        public static float GetBlackLstarScale(float srcBlackL)
        {
            return srcBlackL > 0f && srcBlackL < 100f ? 100f / (100f - srcBlackL) : 1f;
        }

        // Helpers used only during static precomputation
        public static float[,] Multiply3x3(float[,] A, float[,] B)
        {
            if (A == null || B == null)
            {
                return B ?? A;
            }

            var r = new float[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    r[i, j] = A[i, 0] * B[0, j] + A[i, 1] * B[1, j] + A[i, 2] * B[2, j];
                }
            }

            return r;
        }

        // New overload: multiply 3x3 matrix by vector components (x,y,z) -> Vector3.
        // Exposed publicly so color converters can avoid duplicating helpers.
        public static Vector3 Multiply3x3(float[,] m, float x, float y, float z)
        {
            if (m == null || m.Length < 9)
            {
                return new Vector3(x, y, z);
            }

            float rx = m[0, 0] * x + m[0, 1] * y + m[0, 2] * z;
            float ry = m[1, 0] * x + m[1, 1] * y + m[1, 2] * z;
            float rz = m[2, 0] * x + m[2, 1] * y + m[2, 2] * z;
            return new Vector3(rx, ry, rz);
        }

        private static float ComputeSrgbCompandScalar(float c)
        {
            if (c <= 0f)
            {
                return 0f;
            }

            if (c <= 0.0031308f)
            {
                return 12.92f * c;
            }

            return 1.055f * MathF.Pow(c, 1.0f / 2.4f) - 0.055f;
        }

        public static float[,] CreateBradfordAdaptMatrix(float xs, float ys, float zs, float xd, float yd, float zd)
        {
            // Bradford matrices
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

            // Source and destination cone responses
            float sx = M[0,0]*xs + M[0,1]*ys + M[0,2]*zs;
            float sy = M[1,0]*xs + M[1,1]*ys + M[1,2]*zs;
            float sz = M[2,0]*xs + M[2,1]*ys + M[2,2]*zs;
            float dx = M[0,0]*xd + M[0,1]*yd + M[0,2]*zd;
            float dy = M[1,0]*xd + M[1,1]*yd + M[1,2]*zd;
            float dz = M[2,0]*xd + M[2,1]*yd + M[2,2]*zd;

            float rx = dx / (sx == 0 ? 1e-6f : sx);
            float ry = dy / (sy == 0 ? 1e-6f : sy);
            float rz = dz / (sz == 0 ? 1e-6f : sz);

            float[,] D = new float[,]
            {
                { rx, 0f, 0f },
                { 0f, ry, 0f },
                { 0f, 0f, rz }
            };

            // Compute Minv * D * M
            float[,] DM = Multiply3x3(D, M);
            return Multiply3x3(Minv, DM);
        }
    }
}
