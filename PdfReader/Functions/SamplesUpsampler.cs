using System;

namespace PdfReader.Functions
{
    /// <summary>
    /// Upsamples a 1D uniformly sampled function to a target resolution using Catmull-Rom bicubic interpolation.
    /// Input samples are assumed to be uniformly spaced across [0,1].
    /// Note: Values are not clamped; TRC outputs may validly exceed [0,1].
    /// </summary>
    internal static class SamplesUpsampler
    {
        /// <summary>
        /// Resamples the input samples to the specified target length using Catmull-Rom interpolation.
        /// </summary>
        public static float[] UpsampleTo(float[] source, int targetLength)
        {
            if (source == null || source.Length == 0 || targetLength <= 0)
            {
                return Array.Empty<float>();
            }

            if (source.Length == targetLength)
            {
                return (float[])source.Clone();
            }

            if (source.Length == 1)
            {
                float[] single = new float[targetLength];
                for (int i = 0; i < targetLength; i++)
                {
                    single[i] = source[0];
                }
                return single;
            }

            int n = source.Length;
            float[] dst = new float[targetLength];
            float scale = (n - 1) / (float)(targetLength - 1);

            for (int i = 0; i < targetLength; i++)
            {
                float u = i * scale;
                int i1 = (int)u;
                float t = u - i1;

                int i0 = i1 - 1;
                int i2 = i1 + 1;
                int i3 = i1 + 2;

                if (i1 >= n - 1)
                {
                    i1 = n - 2;
                    i0 = i1 - 1;
                    i2 = i1 + 1;
                    i3 = i2;
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

                float p0 = source[i0];
                float p1 = source[i1];
                float p2 = source[i2];
                float p3 = source[i3];

                dst[i] = CatmullRom(p0, p1, p2, p3, t);
            }

            return dst;
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float a0 = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
            float a1 = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
            float a2 = -0.5f * p0 + 0.5f * p2;
            float a3 = p1;
            return a0 * t3 + a1 * t2 + a2 * t + a3;
        }
    }
}
