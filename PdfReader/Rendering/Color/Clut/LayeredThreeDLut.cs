using System;
using System.Numerics;
using PdfReader.Models;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Layered CMY 3D LUT representation sampled at multiple K (black) levels with linear interpolation across K.
    /// Each slice is a regular 3D grid (same layout as TreeDLut) built with CMY varying and fixed K.
    /// Sampling: full trilinear interpolation (C,M,Y) inside two adjacent K slices using Vector3 LUTs, then linear blend across K.
    /// </summary>
    internal sealed class LayeredThreeDLut
    {
        private readonly Vector3[][] _kSlices; // Array of CMY 3D LUTs (one per sampled K). Each slice stores packed Vector3 RGB values.
        private readonly float[] _kLevels;     // Normalized K level positions for slices (0..1 ascending).

        private LayeredThreeDLut(Vector3[][] kSlices, float[] kLevels)
        {
            _kSlices = kSlices;
            _kLevels = kLevels;
        }

        /// <summary>
        /// Builds layered CMY LUTs for a predefined set of K sample levels.
        /// Stores each slice as a Vector3[] (R,G,B) lattice for trilinear sampling.
        /// </summary>
        /// <param name="intent">Rendering intent controlling device to sRGB conversion.</param>
        /// <param name="converter">Delegate converting CMYK (normalized 0..1) to sRGB SKColor.</param>
        internal static LayeredThreeDLut Build(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            // Tunable K sampling distribution (denser near ends for smoother highlight / shadow transitions).
            float[] kLevels = new float[] { 0f, 0.05f, 0.15f, 0.30f, 0.50f, 0.70f, 0.85f, 1.0f };
            int sliceCount = kLevels.Length;
            var slices = new Vector3[sliceCount][];

            int gridSize = TreeDLut.GridSize; // 17
            int pointCount = gridSize * gridSize * gridSize; // 4913

            for (int sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
            {
                float kValue = kLevels[sliceIndex];
                var slice = new Vector3[pointCount];
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

                            slice[writeIndex] = new Vector3(color.Red, color.Green, color.Blue);
                            writeIndex++;
                        }
                    }
                }

                slices[sliceIndex] = slice;
            }

            return new LayeredThreeDLut(slices, kLevels);
        }

        /// <summary>
        /// In-place sampling for a row of interleaved CMYK pixels producing interleaved RGBA output.
        /// Input layout: C,M,Y,K repeating per pixel. Output layout: R,G,B,A.
        /// Performs trilinear (C,M,Y) interpolation inside two adjacent K slices then linear blend across K.
        /// Preserves original K (input alpha channel) instead of forcing opaque, since callers may need it.
        /// </summary>
        /// <param name="rgbaRow">Pointer to first byte of interleaved CMYK input / RGBA output buffer.</param>
        /// <param name="pixelCount">Number of pixels in the buffer.</param>
        internal unsafe void SampleRgbaInPlace(byte* rgbaRow, int pixelCount)
        {
            if (_kSlices == null)
            {
                return; // LUT not initialized.
            }
            if (rgbaRow == null)
            {
                return; // Buffer null.
            }
            if (pixelCount <= 0)
            {
                return; // Nothing to process.
            }

            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                byte* pixelPtr = rgbaRow + (pixelIndex * 4);

                byte cByte = pixelPtr[0];
                byte mByte = pixelPtr[1];
                byte yByte = pixelPtr[2];
                byte kByte = pixelPtr[3];

                float kNormalized = kByte / 255f;

                int upperSliceIndex = 0;
                while (upperSliceIndex < _kLevels.Length && _kLevels[upperSliceIndex] < kNormalized)
                {
                    upperSliceIndex++;
                }

                Rgba sourcePixel = new Rgba(cByte, mByte, yByte, kByte); // Rgba used as CMYK container (R=C,G=M,B=Y,A=K).

                if (upperSliceIndex == 0)
                {
                    Vector3[] firstSlice = _kSlices[0];
                    fixed (Vector3* firstPtr = firstSlice)
                    {
                        TreeDLut.SampleTrilinear(firstPtr, &sourcePixel, (Rgba*)pixelPtr);
                    }
                }
                else if (upperSliceIndex >= _kLevels.Length)
                {
                    Vector3[] lastSlice = _kSlices[_kLevels.Length - 1];
                    fixed (Vector3* lastPtr = lastSlice)
                    {
                        TreeDLut.SampleTrilinear(lastPtr, &sourcePixel, (Rgba*)pixelPtr);
                    }
                }
                else
                {
                    int lowerSliceIndex = upperSliceIndex - 1;
                    Vector3[] lowerSlice = _kSlices[lowerSliceIndex];
                    Vector3[] upperSlice = _kSlices[upperSliceIndex];

                    float kLower = _kLevels[lowerSliceIndex];
                    float kUpper = _kLevels[upperSliceIndex];
                    float t = kUpper <= kLower ? 0f : (kNormalized - kLower) / (kUpper - kLower);
                    if (t < 0f)
                    {
                        t = 0f;
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                    }

                    Rgba lowerSample = default;
                    Rgba upperSample = default;
                    fixed (Vector3* lowerPtr = lowerSlice)
                    fixed (Vector3* upperPtr = upperSlice)
                    {
                        TreeDLut.SampleTrilinear(lowerPtr, &sourcePixel, &lowerSample);
                        TreeDLut.SampleTrilinear(upperPtr, &sourcePixel, &upperSample);
                    }

                    float blendLowerWeight = 1f - t;
                    float blendUpperWeight = t;

                    // Vectorized K-slice blending.
                    Vector3 lowerVector = new Vector3(lowerSample.R, lowerSample.G, lowerSample.B);
                    Vector3 upperVector = new Vector3(upperSample.R, upperSample.G, upperSample.B);
                    Vector3 blendedVector = (lowerVector * blendLowerWeight) + (upperVector * blendUpperWeight);

                    pixelPtr[0] = (byte)(blendedVector.X + 0.5f);
                    pixelPtr[1] = (byte)(blendedVector.Y + 0.5f);
                    pixelPtr[2] = (byte)(blendedVector.Z + 0.5f);
                }

                pixelPtr[3] = kByte; // Preserve original K instead of forcing opaque.
            }
        }
    }
}
