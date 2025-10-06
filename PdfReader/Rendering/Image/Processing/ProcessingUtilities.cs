using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Utility helpers used during PDF image processing for:
    ///  * Building /Decode lookup tables (per component) in the RAW sample code domain.
    ///  * Creating alpha and image mask lookup tables.
    ///  * Building color key masking ranges (inclusive min / max per component).
    ///  * Building indexed color decode maps (RAW code -> palette index).
    /// RAW sample domains:
    ///  * 1 bpc  -> 0..1
    ///  * 2 bpc  -> 0..3
    ///  * 4 bpc  -> 0..15
    ///  * 8 bpc  -> 0..255
    ///  * 16 bpc -> high byte only (0..255)
    /// NOTE: Color key mask ranges produced by <see cref="TryBuildColorKeyRanges"/> are scaled to the 8‑bit domain (0..255)
    /// for all bit depths so they can be compared directly against decoded 8‑bit component values produced by the
    /// byte decode path. (For 16 bpc we downscale to high byte; for < 8 bpc we linearly scale raw code to 0..255.)
    /// </summary>
    internal static class ProcessingUtilities
    {
        private const float DecodeEqualityEpsilon = 1e-12f;
        private const int EightBitDomainSize = 256;
        private const int MaxByte = 255;

        /// <summary>
        /// Returns true when a /Decode array indicates that mapping must be applied
        /// (i.e. differs from the canonical [0 1] for the first component).
        /// </summary>
        public static bool ApplyDecode(IReadOnlyList<float> decodeArray)
        {
            if (decodeArray != null && decodeArray.Count >= 2)
            {
                bool differsFromDefault = Math.Abs(decodeArray[0] - 0f) > 1e-6f || Math.Abs(decodeArray[1] - 1f) > 1e-6f;
                if (differsFromDefault)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Build per-component decode lookup tables from an optional /Decode array.
        /// The resulting LUTs map raw sample codes to normalized floats in [0,1].
        /// </summary>
        public static float[][] BuildDecodeLuts(int componentCount, int bitsPerComponent, IReadOnlyList<float> decodeArray)
        {
            if (componentCount <= 0)
            {
                return Array.Empty<float[]>();
            }

            bool hasDecode = decodeArray != null && decodeArray.Count >= componentCount * 2;

            int lutSize = GetRawDomainSize(bitsPerComponent);
            var luts = new float[componentCount][];

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                float decodeMin = 0f;
                float decodeMax = 1f;

                if (hasDecode)
                {
                    decodeMin = decodeArray[componentIndex * 2];
                    decodeMax = decodeArray[componentIndex * 2 + 1];
                }

                if (IsConstantRange(decodeMin, decodeMax))
                {
                    float constant = Clamp01(decodeMin);
                    float[] constantLut = new float[lutSize];
                    for (int i = 0; i < lutSize; i++)
                    {
                        constantLut[i] = constant;
                    }
                    luts[componentIndex] = constantLut;
                    continue;
                }

                float[] lut = new float[lutSize];
                float maxCode = lutSize - 1;
                for (int code = 0; code < lutSize; code++)
                {
                    float normalized = maxCode <= 0 ? 0f : code / maxCode;
                    float mapped = decodeMin + normalized * (decodeMax - decodeMin);
                    mapped = Clamp01(mapped);
                    lut[code] = mapped;
                }
                luts[componentIndex] = lut;
            }

            return luts;
        }

        /// <summary>
        /// Build per-component decode lookup tables from an optional /Decode array, outputting byte components (0..255).
        /// Each LUT maps raw sample codes directly to an 8-bit component value, avoiding per-pixel float math.
        /// This is used by the byte-based color conversion path (PdfColorSpaceConverter.ToSrgb8Bit).
        /// </summary>
        /// <param name="componentCount">Number of color components.</param>
        /// <param name="bitsPerComponent">Bits per component (1,2,4,8,16 supported; 16 uses the high byte domain).</param>
        /// <param name="decodeArray">Optional /Decode array ([d0 d1] per component) or null for identity [0 1].</param>
        /// <returns>Array of per-component byte LUTs (raw code -> component byte 0..255).</returns>
        public static byte[][] Build8BitDecodeLuts(int componentCount, int bitsPerComponent, IReadOnlyList<float> decodeArray)
        {
            if (componentCount <= 0)
            {
                return Array.Empty<byte[]>();
            }

            bool hasDecode = decodeArray != null && decodeArray.Count >= componentCount * 2;
            int lutSize = GetRawDomainSize(bitsPerComponent);
            var luts = new byte[componentCount][];

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                float decodeMin = 0f;
                float decodeMax = 1f;
                if (hasDecode)
                {
                    decodeMin = decodeArray[componentIndex * 2];
                    decodeMax = decodeArray[componentIndex * 2 + 1];
                }

                if (IsConstantRange(decodeMin, decodeMax))
                {
                    byte constantByte = (byte)(Clamp01(decodeMin) * 255f + 0.5f);
                    byte[] constantLut = new byte[lutSize];
                    for (int i = 0; i < lutSize; i++)
                    {
                        constantLut[i] = constantByte;
                    }
                    luts[componentIndex] = constantLut;
                    continue;
                }

                byte[] lut = new byte[lutSize];
                float maxCode = lutSize - 1;
                float span = decodeMax - decodeMin;
                for (int rawCode = 0; rawCode < lutSize; rawCode++)
                {
                    float normalized = maxCode <= 0 ? 0f : rawCode / maxCode; // 0..1
                    float mapped = decodeMin + normalized * span; // decode domain
                    mapped = Clamp01(mapped);
                    lut[rawCode] = (byte)(mapped * 255f + 0.5f);
                }
                luts[componentIndex] = lut;
            }

            return luts;
        }

        /// <summary>
        /// Attempt to build per-component color key mask ranges (inclusive min/max) from a /Mask array.
        /// Returned ranges are scaled to 0..255 for every supported bits-per-component value so that they can be directly
        /// compared against decoded 8-bit component values. Scaling rules:
        ///  * 16 bpc: high byte (value >> 8) already yields 0..255.
        ///  * 8 bpc: unchanged.
        ///  * 1/2/4 bpc: linearly scaled (raw * 255 / rawMax) with rounding to nearest.
        /// </summary>
        /// <param name="componentCount">Number of color components in the source image.</param>
        /// <param name="bitsPerComponent">Bits per component (1,2,4,8,16 supported).</param>
        /// <param name="maskArray">/Mask numeric array specifying min/max pairs.</param>
        /// <param name="minInclusive">Output: minimum inclusive values per component.</param>
        /// <param name="maxInclusive">Output: maximum inclusive values per component.</param>
        /// <returns>True if ranges were constructed; false if the array was invalid.</returns>
        public static bool TryBuildColorKeyRanges(int componentCount, int bitsPerComponent, IReadOnlyList<float> maskArray, out int[] minInclusive, out int[] maxInclusive)
        {
            minInclusive = null;
            maxInclusive = null;

            if (maskArray == null || maskArray.Count < componentCount * 2)
            {
                return false;
            }

            int rawMax;
            bool is16 = bitsPerComponent == 16;
            if (is16)
            {
                rawMax = 65535; // will shift to 0..255
            }
            else if (bitsPerComponent == 8)
            {
                rawMax = 255; // direct
            }
            else if (bitsPerComponent == 1 || bitsPerComponent == 2 || bitsPerComponent == 4)
            {
                rawMax = (1 << bitsPerComponent) - 1; // 1,3,15
            }
            else
            {
                // Unsupported depth: fall back to 8-bit assumptions
                rawMax = 255;
            }

            minInclusive = new int[componentCount];
            maxInclusive = new int[componentCount];

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int baseIndex = componentIndex * 2;
                int minRaw = (int)Math.Round(maskArray[baseIndex]);
                int maxRaw = (int)Math.Round(maskArray[baseIndex + 1]);
                if (minRaw > maxRaw)
                {
                    int temp = minRaw;
                    minRaw = maxRaw;
                    maxRaw = temp;
                }

                if (is16)
                {
                    minRaw = Clamp(minRaw, 0, 65535) >> 8; // high byte
                    maxRaw = Clamp(maxRaw, 0, 65535) >> 8;
                }
                else if (bitsPerComponent == 8)
                {
                    minRaw = Clamp(minRaw, 0, 255);
                    maxRaw = Clamp(maxRaw, 0, 255);
                }
                else if (bitsPerComponent == 1 || bitsPerComponent == 2 || bitsPerComponent == 4)
                {
                    // Scale small raw domain to 0..255 preserving endpoints.
                    int domainMax = rawMax; // 1,3,15
                    minRaw = Clamp(minRaw, 0, domainMax);
                    maxRaw = Clamp(maxRaw, 0, domainMax);
                    // Scale with rounding: value * 255 / domainMax
                    minRaw = domainMax == 0 ? 0 : (minRaw * 255 + (domainMax / 2)) / domainMax;
                    maxRaw = domainMax == 0 ? 0 : (maxRaw * 255 + (domainMax / 2)) / domainMax;
                }
                else
                {
                    // Fallback clamp.
                    minRaw = Clamp(minRaw, 0, 255);
                    maxRaw = Clamp(maxRaw, 0, 255);
                }

                minInclusive[componentIndex] = minRaw;
                maxInclusive[componentIndex] = maxRaw;
            }

            return true;
        }

        /// <summary>
        /// Build a mapping from raw sample code (bit-packed index) to palette index for an Indexed color space.
        /// The PDF spec defines the default decode range for Indexed images as [0 hiVal] where hiVal = paletteLength - 1.
        /// If a /Decode array is supplied its first two numbers (decode[0], decode[1]) are used as an alternate linear mapping range.
        /// Mapping steps:
        ///  1. Determine raw domain size from bitsPerComponent (2^bpc or 256 for 16 bpc high-byte usage).
        ///  2. For each raw code c in [0, rawMax] compute normalized = c / rawMax (unless rawMax==0).
        ///  3. decoded = decodeMin + normalized * (decodeMax - decodeMin).
        ///  4. Truncate (floor for positive values) to integer palette index and clamp to [0, hiVal].
        /// Fast paths:
        ///  * Identity decode: [0 hiVal] -> paletteIndex = min(rawCode, hiVal).
        ///  * Constant decode (decodeMin ≈ decodeMax): all entries map to same clamped index.
        /// </summary>
        /// <param name="paletteLength">Number of entries in the palette (must be > 0).</param>
        /// <param name="bitsPerComponent">Bits per component of the indexed image samples.</param>
        /// <param name="decodeArray">Optional /Decode array; only the first two values are relevant for Indexed.</param>
        /// <returns>Int array mapping every raw code (array index) to a palette index.</returns>
        public static int[] BuildIndexedDecodeMap(int paletteLength, int bitsPerComponent, IReadOnlyList<float> decodeArray)
        {
            if (paletteLength <= 0)
            {
                return Array.Empty<int>();
            }

            int hiVal = paletteLength - 1;
            int rawDomainSize = GetRawDomainSize(bitsPerComponent);
            int rawMax = rawDomainSize - 1;
            int[] indexMap = new int[rawDomainSize];

            float decodeMin = 0f;
            float decodeMax = hiVal;
            bool hasDecode = decodeArray != null && decodeArray.Count >= 2;
            if (hasDecode)
            {
                decodeMin = decodeArray[0];
                decodeMax = decodeArray[1];
            }

            // Fast path: canonical identity mapping [0 hiVal]
            if (Math.Abs(decodeMin - 0f) < DecodeEqualityEpsilon && Math.Abs(decodeMax - hiVal) < DecodeEqualityEpsilon)
            {
                for (int rawCode = 0; rawCode <= rawMax; rawCode++)
                {
                    int paletteIndex = rawCode <= hiVal ? rawCode : hiVal;
                    indexMap[rawCode] = paletteIndex;
                }
                return indexMap;
            }

            // Constant mapping (all values collapse to one index)
            if (IsConstantRange(decodeMin, decodeMax))
            {
                int singleIndex = (int)(decodeMin >= 0 ? Math.Floor(decodeMin) : Math.Ceiling(decodeMin));
                if (singleIndex < 0)
                {
                    singleIndex = 0;
                }
                else if (singleIndex > hiVal)
                {
                    singleIndex = hiVal;
                }

                for (int rawCode = 0; rawCode <= rawMax; rawCode++)
                {
                    indexMap[rawCode] = singleIndex;
                }
                return indexMap;
            }

            float span = decodeMax - decodeMin;
            float denom = rawMax == 0 ? 1f : rawMax;

            for (int rawCode = 0; rawCode <= rawMax; rawCode++)
            {
                float normalized = rawCode / denom; // 0..1
                float decodedValue = decodeMin + normalized * span;
                int paletteIndex = (int)Math.Floor(decodedValue); // truncate toward -infinity (decode values usually non-negative)
                if (paletteIndex < 0)
                {
                    paletteIndex = 0;
                }
                else if (paletteIndex > hiVal)
                {
                    paletteIndex = hiVal;
                }
                indexMap[rawCode] = paletteIndex;
            }

            return indexMap;
        }

        /// <summary>
        /// Build a 2-entry LUT (RAW code 0/1) for an image mask using /Decode ordering.
        /// Default [0 1] => 0 -> opaque white, 1 -> transparent (0 alpha). Inverted when [1 0].
        /// </summary>
        public static byte[] BuildImageMaskLut(IReadOnlyList<float> decodeArray)
        {
            bool invert = false;
            if (decodeArray != null && decodeArray.Count >= 2)
            {
                invert = decodeArray[0] > decodeArray[1];
            }

            byte[] alphaLut = new byte[2];
            alphaLut[0] = invert ? (byte)0 : (byte)255;
            alphaLut[1] = invert ? (byte)255 : (byte)0;

            return alphaLut;
        }

        /// <summary>
        /// Build an alpha LUT mapping RAW sample codes to 0..255 alpha values using the first decode interval.
        /// </summary>
        public static byte[] BuildAlphaLut(int bitsPerComponent, IReadOnlyList<float> decodeArray)
        {
            var (decodeMin, decodeMax) = GetMinMaxOrDefault(decodeArray);

            int lutSize = GetRawDomainSize(bitsPerComponent);
            byte[] lut = new byte[lutSize];

            if (IsConstantRange(decodeMin, decodeMax))
            {
                byte constant = (byte)(Clamp01(decodeMin) * 255f + 0.5f);
                for (int i = 0; i < lutSize; i++)
                {
                    lut[i] = constant;
                }
                return lut;
            }

            float maxCode = lutSize - 1;
            for (int code = 0; code < lutSize; code++)
            {
                float normalized = maxCode <= 0 ? 0f : code / maxCode;
                float alpha = decodeMin + normalized * (decodeMax - decodeMin);
                alpha = Clamp01(alpha);
                lut[code] = (byte)(alpha * 255f + 0.5f);
            }
            return lut;
        }

        private static int GetRawDomainSize(int bitsPerComponent)
        {
            // Special case for 16 bpc: we only operate on the high byte so domain is 256.
            if (bitsPerComponent == 16)
            {
                return EightBitDomainSize;
            }

            if (bitsPerComponent >= 1 && bitsPerComponent <= 8)
            {
                // For 1,2,4,8 bits we can compute the domain size via left shift (1 << bpc).
                // This naturally produces: 2,4,16,256.
                return 1 << bitsPerComponent;
            }

            // Fallback: treat as 8-bit domain size.
            return EightBitDomainSize;
        }

        private static (float decodeMin, float decodeMax) GetMinMaxOrDefault(IReadOnlyList<float> decodeArray)
        {
            float decodeMin = 0f;
            float decodeMax = 1f;
            if (decodeArray != null && decodeArray.Count >= 2)
            {
                decodeMin = decodeArray[0];
                decodeMax = decodeArray[1];
            }

            return (decodeMin, decodeMax);
        }

        private static bool IsConstantRange(float min, float max)
        {
            return Math.Abs(max - min) < DecodeEqualityEpsilon;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 1f)
            {
                return 1f;
            }
            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}