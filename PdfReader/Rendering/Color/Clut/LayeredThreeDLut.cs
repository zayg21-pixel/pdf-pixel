using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Layered CMY 3D LUT representation sampled at multiple K (black) levels with linear interpolation across K.
    /// Each slice is a regular 3D grid (same layout as IccRgb3dLut) built with CMY fixed and varying.
    /// Sampling: bilinear in RG with nearest B (delegated to TreeDLut) inside the two adjacent K slices, then linear blend.
    /// </summary>
    internal sealed class LayeredThreeDLut
    {
        private readonly byte[][] _kSlices; // Array of CMY 3D LUTs (one per sampled K). Each slice stores packed RGB bytes.
        private readonly float[] _kLevels;  // Normalized K level positions for slices (0..1 ascending).

        private LayeredThreeDLut(byte[][] kSlices, float[] kLevels)
        {
            _kSlices = kSlices;
            _kLevels = kLevels;
        }

        /// <summary>
        /// Build layered CMY LUTs for a set of K sample levels.
        /// </summary>
        internal static LayeredThreeDLut Build(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            float[] kLevels = new float[] { 0f, 0.05f, 0.15f, 0.30f, 0.50f, 0.70f, 0.85f, 1.0f };
            int sliceCount = kLevels.Length;
            byte[][] slices = new byte[sliceCount][];

            for (int sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
            {
                float kValue = kLevels[sliceIndex];
                int gridSize = TreeDLut.GridSize;
                int pointCount = gridSize * gridSize * gridSize;
                int sliceByteCount = pointCount * 3;
                byte[] slice = new byte[sliceByteCount];
                int writeIndex = 0;

                for (int cIndex = 0; cIndex < gridSize; cIndex++)
                {
                    float c = (float)cIndex / (gridSize - 1);
                    for (int mIndex = 0; mIndex < gridSize; mIndex++)
                    {
                        float m = (float)mIndex / (gridSize - 1);
                        for (int yIndex = 0; yIndex < gridSize; yIndex++)
                        {
                            float y = (float)yIndex / (gridSize - 1);
                            ReadOnlySpan<float> components = stackalloc float[] { c, m, y, kValue };
                            var color = converter(components, intent);

                            slice[writeIndex++] = color.Red;
                            slice[writeIndex++] = color.Green;
                            slice[writeIndex++] = color.Blue;
                        }
                    }
                }

                slices[sliceIndex] = slice;
            }

            return new LayeredThreeDLut(slices, kLevels);
        }

        /// <summary>
        /// In-place sampling for a row of interleaved CMYK pixels producing interleaved RGBA output.
        /// Input layout: C,M,Y,K repeating per pixel. Output layout: R,G,B,Alpha (alpha forced to 255).
        /// Performs bilinear CMY interpolation inside two adjacent K slices then linear blend across K.
        /// </summary>
        /// <param name="rgbaRow">Pointer to first byte of interleaved CMYK input / RGBA output buffer.</param>
        /// <param name="pixelCount">Number of pixels in the buffer.</param>
        internal unsafe void SampleRgbaInPlace(byte* rgbaRow, int pixelCount)
        {
            if (_kSlices == null)
            {
                return;
            }
            if (rgbaRow == null)
            {
                return;
            }
            if (pixelCount <= 0)
            {
                return;
            }

            byte* temp = stackalloc byte[6]; // lower(0..2) | upper(3..5)
            byte* lowerRgb = temp;
            byte* upperRgb = temp + 3;

            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                byte* pixelPtr = rgbaRow + (pixelIndex * 4);

                byte c = pixelPtr[0];
                byte m = pixelPtr[1];
                byte y = pixelPtr[2];
                byte k = pixelPtr[3];

                float kNormalized = k / 255f;

                int upperIndex = 0;
                while (upperIndex < _kLevels.Length && _kLevels[upperIndex] < kNormalized)
                {
                    upperIndex++;
                }

                if (upperIndex == 0)
                {
                    byte[] first = _kSlices[0];
                    TreeDLut.SampleBilinear8(first, c, m, y, pixelPtr);
                }
                else if (upperIndex >= _kLevels.Length)
                {
                    byte[] last = _kSlices[_kLevels.Length - 1];

                    TreeDLut.SampleBilinear8(last, c, m, y, pixelPtr);
                }
                else
                {
                    int lowerIndex = upperIndex - 1;
                    byte[] sliceLower = _kSlices[lowerIndex];
                    byte[] sliceUpper = _kSlices[upperIndex];

                    float k0 = _kLevels[lowerIndex];
                    float k1 = _kLevels[upperIndex];
                    float t = k1 <= k0 ? 0f : (kNormalized - k0) / (k1 - k0);
                    if (t < 0f)
                    {
                        t = 0f;
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                    }

                    TreeDLut.SampleBilinear8(sliceLower, c, m, y, lowerRgb);
                    TreeDLut.SampleBilinear8(sliceUpper, c, m, y, upperRgb);

                    float oneMinusT = 1f - t;
                    pixelPtr[0] = (byte)(lowerRgb[0] * oneMinusT + upperRgb[0] * t + 0.5f);
                    pixelPtr[1] = (byte)(lowerRgb[1] * oneMinusT + upperRgb[1] * t + 0.5f);
                    pixelPtr[2] = (byte)(lowerRgb[2] * oneMinusT + upperRgb[2] * t + 0.5f);
                }

                pixelPtr[3] = 255; // alpha
            }
        }
    }
}
