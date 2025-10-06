using System;
using System.Numerics;
using PdfReader.Models;

namespace PdfReader.Icc
{
    /// <summary>
    /// Layered CMY 3D LUT representation sampled at multiple K (black) levels with linear interpolation across K.
    /// Each slice is a regular 3D grid (same layout as IccRgb3dLut) built with CMY fixed and varying.
    /// Sampling: trilinear (RG + nearest B optional variant; here full trilinear) inside the two adjacent K slices, then linear blend.
    /// </summary>
    internal sealed class IccCmykLayered3dLut
    {
        private readonly Vector3[][] _kSlices; // Array of CMY 3D LUTs (one per sampled K).
        private readonly float[] _kLevels;     // Normalized K level positions for slices (0..1 ascending).
        private readonly bool _hasData;

        internal bool HasSlices
        {
            get { return _hasData; }
        }

        private IccCmykLayered3dLut(Vector3[][] kSlices, float[] kLevels, bool hasData)
        {
            _kSlices = kSlices;
            _kLevels = kLevels;
            _hasData = hasData;
        }

        /// <summary>
        /// Build layered CMY LUTs for a set of K sample levels. Returns null if all conversions fail.
        /// </summary>
        internal static IccCmykLayered3dLut Build(PdfRenderingIntent intent, CmykDeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            // Choose K sampling levels (denser near shadows and highlights). Tunable constants.
            float[] kLevels = new float[] { 0f, 0.05f, 0.15f, 0.30f, 0.50f, 0.70f, 0.85f, 1.0f };
            int sliceCount = kLevels.Length;
            Vector3[][] slices = new Vector3[sliceCount][];
            bool anySuccess = false;

            for (int si = 0; si < sliceCount; si++)
            {
                float kVal = kLevels[si];
                int n = IccRgb3dLut.GridSize;
                int total = n * n * n;
                Vector3[] slice = new Vector3[total];
                int writeIndex = 0;
                bool sliceSuccess = false;

                for (int cIndex = 0; cIndex < n; cIndex++)
                {
                    float c = (float)cIndex / (n - 1);
                    for (int mIndex = 0; mIndex < n; mIndex++)
                    {
                        float m = (float)mIndex / (n - 1);
                        for (int yIndex = 0; yIndex < n; yIndex++)
                        {
                            float y = (float)yIndex / (n - 1);
                            if (!converter(c, m, y, kVal, intent, out Vector3 srgb))
                            {
                                srgb = Vector3.Zero;
                            }
                            else
                            {
                                sliceSuccess = true;
                            }
                            slice[writeIndex++] = srgb;
                        }
                    }
                }

                slices[si] = sliceSuccess ? slice : null;
                if (sliceSuccess)
                {
                    anySuccess = true;
                }
            }

            if (!anySuccess)
            {
                return null;
            }

            return new IccCmykLayered3dLut(slices, kLevels, true);
        }

        /// <summary>
        /// Sample layered LUT with trilinear CMY interpolation in adjacent K slices then linear K blend.
        /// Falls back to single-slice sampling when one side missing. Returns zero vector if no data.
        /// </summary>
        internal Vector3 Sample(float c, float m, float y, float k, SamlingInterpolation interpolation)
        {
            if (!_hasData)
            {
                return Vector3.Zero;
            }

            // Find K interval.
            int upperIndex = 0;
            for (int i = 0; i < _kLevels.Length; i++)
            {
                if (_kLevels[i] >= k)
                {
                    upperIndex = i;
                    break;
                }
            }

            if (upperIndex == 0)
            {
                Vector3[] first = _kSlices[0];
                if (first == null)
                {
                    return Vector3.Zero;
                }
                return IccRgb3dLut.Sample(first, c, m, y, interpolation);
            }

            if (upperIndex >= _kLevels.Length)
            {
                Vector3[] last = _kSlices[_kLevels.Length - 1];
                if (last == null)
                {
                    return Vector3.Zero;
                }
                return IccRgb3dLut.Sample(last, c, m, y, interpolation);
            }

            int lowerIndex = upperIndex - 1;
            Vector3[] lowerSlice = _kSlices[lowerIndex];
            Vector3[] upperSlice = _kSlices[upperIndex];

            if (lowerSlice == null && upperSlice == null)
            {
                return Vector3.Zero;
            }
            if (lowerSlice != null && upperSlice == null)
            {
                return IccRgb3dLut.Sample(lowerSlice, c, m, y, interpolation);
            }
            if (lowerSlice == null && upperSlice != null)
            {
                return IccRgb3dLut.Sample(upperSlice, c, m, y, interpolation);
            }

            float k0 = _kLevels[lowerIndex];
            float k1 = _kLevels[upperIndex];
            float t = (k - k0) / (k1 - k0);

            Vector3 c0 = IccRgb3dLut.Sample(lowerSlice, c, m, y, interpolation);
            Vector3 c1 = IccRgb3dLut.Sample(upperSlice, c, m, y, interpolation);
            return Vector3.Lerp(c0, c1, t);
        }
    }
}
