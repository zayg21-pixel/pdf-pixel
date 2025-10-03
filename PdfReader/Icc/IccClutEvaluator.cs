using System;

namespace PdfReader.Icc
{
    internal static class IccClutEvaluator
    {
        public static float[] EvaluateClutTetrahedral3D(IccLutPipeline lut, float[] v)
        {
            int grid = lut.GridPoints;
            int outCh = lut.OutChannels;
            float scale = grid - 1;
            float px = v[0] * scale; if (px < 0f) px = 0f; if (px > scale) px = scale;
            float py = v[1] * scale; if (py < 0f) py = 0f; if (py > scale) py = scale;
            float pz = v[2] * scale; if (pz < 0f) pz = 0f; if (pz > scale) pz = scale;
            int ix = (int)Math.Floor(px); float fx = px - ix; int ix1 = ix < grid - 1 ? ix + 1 : ix;
            int iy = (int)Math.Floor(py); float fy = py - iy; int iy1 = iy < grid - 1 ? iy + 1 : iy;
            int iz = (int)Math.Floor(pz); float fz = pz - iz; int iz1 = iz < grid - 1 ? iz + 1 : iz;

            int s2 = outCh;
            int s1 = grid * s2;
            int s0 = grid * s1;

            int o000 = ix * s0 + iy * s1 + iz * s2;
            int o100 = ix1 * s0 + iy * s1 + iz * s2;
            int o010 = ix * s0 + iy1 * s1 + iz * s2;
            int o001 = ix * s0 + iy * s1 + iz1 * s2;
            int o110 = ix1 * s0 + iy1 * s1 + iz * s2;
            int o101 = ix1 * s0 + iy * s1 + iz1 * s2;
            int o011 = ix * s0 + iy1 * s1 + iz1 * s2;
            int o111 = ix1 * s0 + iy1 * s1 + iz1 * s2;

            var acc = new float[outCh];

            if (fx >= fy)
            {
                if (fy >= fz)
                {
                    float w0 = 1f - fx;
                    float w1 = fx - fy;
                    float w2 = fy - fz;
                    float w3 = fz;
                    for (int c = 0; c < outCh; c++) acc[c] = lut.Clut[o000 + c] * w0 + lut.Clut[o100 + c] * w1 + lut.Clut[o110 + c] * w2 + lut.Clut[o111 + c] * w3;
                }
                else if (fx >= fz)
                {
                    float w0 = 1f - fx; float w1 = fx - fz; float w2 = fz - fy; float w3 = fy;
                    for (int c = 0; c < outCh; c++) acc[c] = lut.Clut[o000 + c] * w0 + lut.Clut[o100 + c] * w1 + lut.Clut[o101 + c] * w2 + lut.Clut[o111 + c] * w3;
                }
                else
                {
                    float w0 = 1f - fz; float w1 = fz - fx; float w2 = fx - fy; float w3 = fy;
                    for (int c = 0; c < outCh; c++) acc[c] = lut.Clut[o000 + c] * w0 + lut.Clut[o001 + c] * w1 + lut.Clut[o101 + c] * w2 + lut.Clut[o111 + c] * w3;
                }
            }
            else
            {
                if (fx >= fz)
                {
                    float w0 = 1f - fy; float w1 = fy - fx; float w2 = fx - fz; float w3 = fz;
                    for (int c = 0; c < outCh; c++) acc[c] = lut.Clut[o000 + c] * w0 + lut.Clut[o010 + c] * w1 + lut.Clut[o110 + c] * w2 + lut.Clut[o111 + c] * w3;
                }
                else if (fy >= fz)
                {
                    float w0 = 1f - fy; float w1 = fy - fz; float w2 = fz - fx; float w3 = fx;
                    for (int c = 0; c < outCh; c++) acc[c] = lut.Clut[o000 + c] * w0 + lut.Clut[o010 + c] * w1 + lut.Clut[o011 + c] * w2 + lut.Clut[o111 + c] * w3;
                }
                else
                {
                    float w0 = 1f - fz; float w1 = fz - fy; float w2 = fy - fx; float w3 = fx;
                    for (int c = 0; c < outCh; c++) acc[c] = lut.Clut[o000 + c] * w0 + lut.Clut[o001 + c] * w1 + lut.Clut[o011 + c] * w2 + lut.Clut[o111 + c] * w3;
                }
            }

            return acc;
        }

        public static float[] EvaluateClutLinear(IccLutPipeline lut, float[] vin)
        {
            int n = vin.Length;
            int grid = lut.GridPoints;
            int outCh = lut.OutChannels;

            var idx0 = new int[n];
            var frac = new float[n];
            float scale = grid - 1;
            for (int d = 0; d < n; d++)
            {
                float p = vin[d] * scale;
                if (p < 0f) p = 0f;
                if (p > scale) p = scale;
                int i0 = (int)Math.Floor(p);
                float f = p - i0;
                idx0[d] = i0;
                frac[d] = f;
            }

            var stride = new int[n];
            int s = outCh;
            for (int d = n - 1; d >= 0; d--)
            {
                stride[d] = s;
                s *= grid;
            }

            var acc = new float[outCh];
            int vertices = 1 << n;
            for (int v = 0; v < vertices; v++)
            {
                float w = 1f;
                int offset = 0;
                for (int d = 0; d < n; d++)
                {
                    int bit = (v >> d) & 1;
                    int id = idx0[d] + bit;
                    if (id >= grid) { w = 0f; break; }
                    w *= (bit == 0) ? (1f - frac[d]) : frac[d];
                    offset += id * stride[d];
                }
                if (w == 0f) continue;

                int baseIndex = offset;
                for (int c = 0; c < outCh; c++)
                {
                    acc[c] += lut.Clut[baseIndex + c] * w;
                }
            }

            return acc;
        }

        public static float[] EvaluateClutLinearMab(IccLutPipeline p, float[] vin)
        {
            int n = p.GridPointsPerDim?.Length ?? p.InChannels;
            int outCh = p.OutChannels;

            var idx0 = new int[n];
            var frac = new float[n];
            for (int d = 0; d < n; d++)
            {
                int grid = p.GridPointsPerDim[d];
                float scale = grid - 1;
                float pos = vin[d] * scale;
                if (pos < 0f) pos = 0f; if (pos > scale) pos = scale;
                int i0 = (int)Math.Floor(pos);
                idx0[d] = i0;
                frac[d] = pos - i0;
            }

            var stride = new int[n];
            int s = outCh;
            for (int d = n - 1; d >= 0; d--)
            {
                stride[d] = s;
                s *= p.GridPointsPerDim[d];
            }

            var acc = new float[outCh];
            int vertices = 1 << n;
            for (int v = 0; v < vertices; v++)
            {
                float w = 1f;
                int offset = 0;
                for (int d = 0; d < n; d++)
                {
                    int grid = p.GridPointsPerDim[d];
                    int bit = (v >> d) & 1;
                    int id = idx0[d] + bit;
                    if (id >= grid) { w = 0f; break; }
                    w *= (bit == 0) ? (1f - frac[d]) : frac[d];
                    offset += id * stride[d];
                }
                if (w == 0f) continue;

                for (int c = 0; c < outCh; c++)
                {
                    acc[c] += p.Clut[offset + c] * w;
                }
            }

            return acc;
        }

        public static float[] EvaluateClutTetrahedral3DMab(IccLutPipeline p, float[] v)
        {
            int outCh = p.OutChannels;
            int gx = p.GridPointsPerDim[0];
            int gy = p.GridPointsPerDim[1];
            int gz = p.GridPointsPerDim[2];

            float sx = gx - 1; float px = v[0] * sx; if (px < 0f) px = 0f; if (px > sx) px = sx;
            float sy = gy - 1; float py = v[1] * sy; if (py < 0f) py = 0f; if (py > sy) py = sy;
            float sz = gz - 1; float pz = v[2] * sz; if (pz < 0f) pz = 0f; if (pz > sz) pz = sz;

            int ix = (int)Math.Floor(px); float fx = px - ix; int ix1 = ix < gx - 1 ? ix + 1 : ix;
            int iy = (int)Math.Floor(py); float fy = py - iy; int iy1 = iy < gy - 1 ? iy + 1 : iy;
            int iz = (int)Math.Floor(pz); float fz = pz - iz; int iz1 = iz < gz - 1 ? iz + 1 : iz;

            int s2 = outCh;
            int s1 = gz * s2;
            int s0 = gy * s1;

            int o000 = ix * s0 + iy * s1 + iz * s2;
            int o100 = ix1 * s0 + iy * s1 + iz * s2;
            int o010 = ix * s0 + iy1 * s1 + iz * s2;
            int o001 = ix * s0 + iy * s1 + iz1 * s2;
            int o110 = ix1 * s0 + iy1 * s1 + iz * s2;
            int o101 = ix1 * s0 + iy * s1 + iz1 * s2;
            int o011 = ix * s0 + iy1 * s1 + iz1 * s2;
            int o111 = ix1 * s0 + iy1 * s1 + iz1 * s2;

            var acc = new float[outCh];

            if (fx >= fy)
            {
                if (fy >= fz)
                {
                    float w0 = 1f - fx; float w1 = fx - fy; float w2 = fy - fz; float w3 = fz;
                    for (int c = 0; c < outCh; c++) acc[c] = p.Clut[o000 + c] * w0 + p.Clut[o100 + c] * w1 + p.Clut[o110 + c] * w2 + p.Clut[o111 + c] * w3;
                }
                else if (fx >= fz)
                {
                    float w0 = 1f - fx; float w1 = fx - fz; float w2 = fz - fy; float w3 = fy;
                    for (int c = 0; c < outCh; c++) acc[c] = p.Clut[o000 + c] * w0 + p.Clut[o100 + c] * w1 + p.Clut[o101 + c] * w2 + p.Clut[o111 + c] * w3;
                }
                else
                {
                    float w0 = 1f - fz; float w1 = fz - fx; float w2 = fx - fy; float w3 = fy;
                    for (int c = 0; c < outCh; c++) acc[c] = p.Clut[o000 + c] * w0 + p.Clut[o001 + c] * w1 + p.Clut[o101 + c] * w2 + p.Clut[o111 + c] * w3;
                }
            }
            else
            {
                if (fx >= fz)
                {
                    float w0 = 1f - fy; float w1 = fy - fx; float w2 = fx - fz; float w3 = fz;
                    for (int c = 0; c < outCh; c++) acc[c] = p.Clut[o000 + c] * w0 + p.Clut[o010 + c] * w1 + p.Clut[o110 + c] * w2 + p.Clut[o111 + c] * w3;
                }
                else if (fy >= fz)
                {
                    float w0 = 1f - fy; float w1 = fy - fz; float w2 = fz - fx; float w3 = fx;
                    for (int c = 0; c < outCh; c++) acc[c] = p.Clut[o000 + c] * w0 + p.Clut[o010 + c] * w1 + p.Clut[o011 + c] * w2 + p.Clut[o111 + c] * w3;
                }
                else
                {
                    float w0 = 1f - fz; float w1 = fz - fy; float w2 = fy - fx; float w3 = fx;
                    for (int c = 0; c < outCh; c++) acc[c] = p.Clut[o000 + c] * w0 + p.Clut[o001 + c] * w1 + p.Clut[o011 + c] * w2 + p.Clut[o111 + c] * w3;
                }
            }

            return acc;
        }
    }
}
