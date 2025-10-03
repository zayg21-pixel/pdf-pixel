using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.PostProcessing
{
    /// <summary>
    /// Utilities for post-processing PDF images: building /Decode LUTs, color key mask ranges, indexed decode maps, and alpha LUTs.
    ///
    /// RAW domain strategy:
    ///  * 1 bpc  -> codes 0..1   (LUT length 2)
    ///  * 2 bpc  -> codes 0..3   (LUT length 4)
    ///  * 4 bpc  -> codes 0..15  (LUT length 16)
    ///  * 8 bpc  -> codes 0..255 (LUT length 256)
    ///  * 16 bpc -> high byte only (codes 0..255, LUT length 256)
    ///
    /// The post processor now supplies raw sample codes following the above domain rules. No synthetic expansion
    /// of sub-8-bit samples to 0..255 occurs prior to LUT indexing. This minimizes work and eliminates the need
    /// to reverse-engineer the original code value for /Decode mapping and color key masking.
    /// </summary>
    internal static class PostProcessingUtilities
    {
        /// <summary>
        /// Build per-component decode LUTs from a /Decode array in the raw sample code domain.
        /// If /Decode is missing or incomplete, an identity mapping in [0,1] is produced.
        /// </summary>
        public static float[][] BuildDecodeLuts(int componentCount, int bitsPerComponent, ReadOnlySpan<float> decode)
        {
            if (componentCount <= 0)
            {
                return Array.Empty<float[]>();
            }

            bool hasDecode = decode != null && decode.Length >= componentCount * 2;

            int lutSize = GetRawDomainSize(bitsPerComponent);
            var luts = new float[componentCount][];

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                float decodeMin = 0f;
                float decodeMax = 1f;
                if (hasDecode)
                {
                    decodeMin = decode[componentIndex * 2];
                    decodeMax = decode[componentIndex * 2 + 1];
                }

                if (Math.Abs(decodeMax - decodeMin) < 1e-12f)
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
                    if (mapped < 0f) { mapped = 0f; }
                    else if (mapped > 1f) { mapped = 1f; }
                    lut[code] = mapped;
                }
                luts[componentIndex] = lut;
            }

            return luts;
        }

        /// <summary>
        /// Try to build color key mask ranges (inclusive min/max) from a /Mask array in the RAW sample domain.
        /// For 16 bpc values are first clamped to 0..65535 then shifted right 8 (high byte domain 0..255).
        /// For sub-8-bit depths domain is 0..(2^bpc-1). No scaling to 0..255 occurs here.
        /// </summary>
        public static bool TryBuildColorKeyRanges(int componentCount, int bitsPerComponent, IReadOnlyList<float> maskArray, out int[] minInclusive, out int[] maxInclusive)
        {
            minInclusive = null;
            maxInclusive = null;

            if (maskArray == null || maskArray.Count < componentCount * 2)
            {
                return false;
            }

            int domainMax;
            bool shiftHigh = false;
            if (bitsPerComponent == 16)
            {
                domainMax = 255; // high byte domain
                shiftHigh = true;
            }
            else if (bitsPerComponent >= 1 && bitsPerComponent <= 8)
            {
                domainMax = bitsPerComponent == 8 ? 255 : (1 << bitsPerComponent) - 1;
            }
            else
            {
                domainMax = 255; // fallback
            }

            minInclusive = new int[componentCount];
            maxInclusive = new int[componentCount];

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int baseIndex = componentIndex * 2;
                int minValue = (int)Math.Round(maskArray[baseIndex]);
                int maxValue = (int)Math.Round(maskArray[baseIndex + 1]);
                if (minValue > maxValue)
                {
                    int t = minValue; minValue = maxValue; maxValue = t;
                }

                if (shiftHigh)
                {
                    if (minValue < 0) { minValue = 0; } else if (minValue > 65535) { minValue = 65535; }
                    if (maxValue < 0) { maxValue = 0; } else if (maxValue > 65535) { maxValue = 65535; }
                    minValue >>= 8;
                    maxValue >>= 8;
                }
                else
                {
                    if (minValue < 0) { minValue = 0; } else if (minValue > domainMax) { minValue = domainMax; }
                    if (maxValue < 0) { maxValue = 0; } else if (maxValue > domainMax) { maxValue = domainMax; }
                }

                minInclusive[componentIndex] = minValue;
                maxInclusive[componentIndex] = maxValue;
            }

            return true;
        }

        /// <summary>
        /// Build indexed color decode mapping from RAW code domain to palette index domain.
        /// RAW domain size follows the same rules as other LUTs (see class summary).
        /// </summary>
        public static int[] BuildIndexedDecodeMap(int paletteLength, int bitsPerComponent, float decodeMin, float decodeMax)
        {
            int rawMax = GetRawDomainSize(bitsPerComponent) - 1;
            int[] indexMap = new int[rawMax + 1];

            if (Math.Abs(decodeMax - decodeMin) < 1e-12f)
            {
                int singleIndex = (int)Math.Round(decodeMin);
                if (singleIndex < 0) { singleIndex = 0; }
                else if (singleIndex >= paletteLength) { singleIndex = paletteLength - 1; }
                for (int code = 0; code <= rawMax; code++)
                {
                    indexMap[code] = singleIndex;
                }
                return indexMap;
            }

            float scale = (decodeMax - decodeMin) / rawMax;
            for (int code = 0; code <= rawMax; code++)
            {
                float mapped = decodeMin + code * scale;
                int paletteIndex = (int)Math.Round(mapped);
                if (paletteIndex < 0) { paletteIndex = 0; }
                else if (paletteIndex >= paletteLength) { paletteIndex = paletteLength - 1; }
                indexMap[code] = paletteIndex;
            }

            return indexMap;
        }

        /// <summary>
        /// Build alpha LUT mapping RAW sample codes to alpha bytes (0..255) using /Decode range.
        /// </summary>
        public static byte[] BuildAlphaLut(int bitsPerComponent, float decodeMin, float decodeMax)
        {
            int lutSize = GetRawDomainSize(bitsPerComponent);
            byte[] lut = new byte[lutSize];

            if (Math.Abs(decodeMax - decodeMin) < 1e-12f)
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
                if (alpha < 0f) { alpha = 0f; }
                else if (alpha > 1f) { alpha = 1f; }
                lut[code] = (byte)(alpha * 255f + 0.5f);
            }
            return lut;
        }

        private static int GetRawDomainSize(int bitsPerComponent)
        {
            if (bitsPerComponent == 16)
            {
                return 256; // high byte domain
            }
            if (bitsPerComponent == 8)
            {
                return 256;
            }
            if (bitsPerComponent == 4)
            {
                return 16;
            }
            if (bitsPerComponent == 2)
            {
                return 4;
            }
            if (bitsPerComponent == 1)
            {
                return 2;
            }
            return 256; // fallback
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) { return 0f; }
            if (value > 1f) { return 1f; }
            return value;
        }
    }
}