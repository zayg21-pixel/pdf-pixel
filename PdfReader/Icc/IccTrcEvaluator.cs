using System;

namespace PdfReader.Icc
{
    internal static class IccTrcEvaluator
    {
        public static float ApplyTrc(IccTrc[] curves, int index, float x)
        {
            if (curves == null || index < 0 || index >= curves.Length) return x;
            return EvaluateTrc(curves[index], x);
        }

        public static float EvaluateTrc(IccTrc c, float x)
        {
            if (c == null) return x;
            if (c.IsGamma) return MathF.Pow(x, c.Gamma);
            if (c.IsSampled)
            {
                var s = c.Samples;
                if (s == null || s.Length == 0) return x;
                float pos = x * (s.Length - 1);
                int i0 = (int)pos;
                if (i0 >= s.Length - 1) return s[s.Length - 1];
                float f = pos - i0;
                return s[i0] + (s[i0 + 1] - s[i0]) * f;
            }
            if (c.IsParametric)
            {
                return ApplyParametric(c.ParametricType, c.Parameters, x);
            }
            return x;
        }

        public static float ApplyParametric(int type, float[] p, float x)
        {
            switch (type)
            {
                case 0:
                {
                    if (p == null || p.Length < 1) return x;
                    float g = p[0];
                    return MathF.Pow(x, g);
                }
                case 1:
                {
                    if (p == null || p.Length < 3) return x;
                    float g = p[0], a = p[1], b = p[2];
                    float x0 = -b / (a == 0 ? 1e-20f : a);
                    if (x < x0) return 0f;
                    return MathF.Pow(a * x + b, g);
                }
                case 2:
                {
                    if (p == null || p.Length < 4) return x;
                    float g = p[0], a = p[1], b = p[2], c0 = p[3];
                    float x0 = -b / (a == 0 ? 1e-20f : a);
                    return (x < x0) ? c0 : MathF.Pow(a * x + b, g) + c0;
                }
                case 3:
                {
                    if (p == null || p.Length < 5) return x;
                    float g = p[0], a = p[1], b = p[2], c1 = p[3], d = p[4];
                    return (x < d) ? c1 * x : MathF.Pow(a * x + b, g);
                }
                case 4:
                {
                    if (p == null || p.Length < 7) return x;
                    float g = p[0], a = p[1], b = p[2], c1 = p[3], d = p[4], e = p[5], f = p[6];
                    return (x < d) ? (c1 * x + f) : MathF.Pow(a * x + b, g) + e;
                }
                default:
                    return x;
            }
        }
    }
}
