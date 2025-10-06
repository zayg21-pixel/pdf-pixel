using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Helper for building and sampling 1D device to sRGB lookup tables (grayscale input to RGB output).
    /// Stores 256 uniformly sampled input points (0..255) each mapped to 3 output bytes (R,G,B).
    /// Provides precomputed conversion tables between byte and normalized float (0..1).
    /// </summary>
    internal static class OneDLut
    {
        internal const int SampleCount = 256;
        private const int BytesPerSample = 3;

        /// <summary>
        /// Precomputed mapping from byte value [0..255] to normalized float in [0,1].
        /// </summary>
        internal static readonly float[] ByteToFloat01;

        /// <summary>
        /// Precomputed mapping from quantized normalized index [0..255] to byte value [0..255].
        /// Useful when the normalized float has already been quantized to an integer 0..255 index.
        /// </summary>
        internal static readonly byte[] Float01ToByte;

        static OneDLut()
        {
            ByteToFloat01 = new float[SampleCount];
            Float01ToByte = new byte[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                ByteToFloat01[i] = i / 255f;
                Float01ToByte[i] = (byte)i;
            }
        }

        /// <summary>
        /// Builds a packed RGB 1D LUT (length 256 * 3) using the provided converter.
        /// Input domain is uniformly sampled at i / 255 for i in [0,255].
        /// Returns null if converter is null.
        /// </summary>
        internal static byte[] Build8Bit(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            int totalBytes = SampleCount * BytesPerSample;
            byte[] lut = new byte[totalBytes];
            int writeIndex = 0;

            for (int i = 0; i < SampleCount; i++)
            {
                float v = ByteToFloat01[i];
                ReadOnlySpan<float> comps = stackalloc float[] { v };
                var color = converter(comps, intent);

                lut[writeIndex++] = color.Red;
                lut[writeIndex++] = color.Green;
                lut[writeIndex++] = color.Blue;
            }

            return lut;
        }

        /// <summary>
        /// Samples the 1D LUT at the exact 8-bit value (no interpolation) and writes 3 RGB bytes.
        /// </summary>
        internal static unsafe void Sample8RgbaInPlace(byte* lut, byte* rgbaRow, int pixelCount)
        {
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                int baseIdx = pixelIndex * 4;
                byte gray = rgbaRow[baseIdx];
                int lutBase = gray * 3;
                rgbaRow[baseIdx] = lut[lutBase];
                rgbaRow[baseIdx + 1] = lut[lutBase + 1];
                rgbaRow[baseIdx + 2] = lut[lutBase + 2];
            }
        }

        /// <summary>
        /// Converts a normalized float [0,1] to a byte using precomputed table. Value is clamped then quantized.
        /// </summary>
        internal static byte ToByte(float value01)
        {
            if (value01 <= 0f)
            {
                return 0;
            }
            if (value01 >= 1f)
            {
                return 255;
            }
            int index = (int)(value01 * 255f + 0.5f);
            return Float01ToByte[index];
        }
    }
}
