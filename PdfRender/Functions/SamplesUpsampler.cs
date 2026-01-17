using System;

namespace PdfRender.Functions
{
    /// <summary>
    /// Upsamples a 1D uniformly sampled function to a target resolution using Catmull-Rom bicubic interpolation.
    /// Input samples are assumed to be uniformly spaced across [0,1].
    /// Note: Values are not clamped; TRC outputs may validly exceed [0,1].
    /// </summary>
    internal static class SamplesUpsampler
    {
        /// <summary>
        /// Resamples a 1D array of samples to the specified target length using Catmull-Rom bicubic interpolation.
        /// Input samples are assumed to be uniformly spaced over [0..1].
        /// </summary>
        public static float[] ResampleCubic(float[] src, int targetLength)
        {
            int n = src.Length;
            if (n == 0 || targetLength <= 0)
            {
                return Array.Empty<float>();
            }
            if (n == 1)
            {
                float[] single = new float[targetLength];
                for (int i = 0; i < targetLength; i++)
                {
                    single[i] = src[0];
                }
                return single;
            }

            float[] dst = new float[targetLength];
            float scale = (n - 1) / (float)(targetLength - 1);

            for (int i = 0; i < targetLength; i++)
            {
                float u = i * scale; // position in source index space
                int i1 = (int)u; // base index
                float t = u - i1; // local fraction

                int i0 = i1 - 1;
                int i2 = i1 + 1;
                int i3 = i1 + 2;

                if (i1 >= n - 1)
                {
                    // Clamp to last segment
                    i1 = n - 2;
                    i0 = i1 - 1;
                    i2 = i1 + 1;
                    i3 = i2; // duplicate last
                    t = 1f;
                }

                if (i0 < 0)
                {
                    i0 = 0;
                }
                if (i3 >= n)
                {
                    i3 = n - 1;
                }

                float p0 = src[i0];
                float p1 = src[i1];
                float p2 = src[i2];
                float p3 = src[i3];

                dst[i] = CatmullRom(p0, p1, p2, p3, t);
            }

            return dst;
        }

        /// <summary>
        /// Catmull-Rom spline interpolation for four successive samples.
        /// </summary>
        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            // Standard Catmull-Rom with tension = 0.5
            float a0 = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
            float a1 = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
            float a2 = -0.5f * p0 + 0.5f * p2;
            float a3 = p1;
            float value = a0 * t3 + a1 * t2 + a2 * t + a3;
            return value;
        }
    }
}
