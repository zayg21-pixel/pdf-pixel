using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Utility helpers used during PDF image processing.
    /// Responsible for building decode mapping arrays, color key mask ranges and related helpers.
    /// </summary>
    internal static class ProcessingUtilities
    {
        /// <summary>
        /// Build a preprocessed /Decode mapping array for per-component application in the row processor.
        /// Returns null when no decode needs to be applied (identity [0 1] for all components or invalid array) and not an image mask.
        /// For each component two bytes are stored: [minByte, maxByte] representing clamped and rounded endpoints.
        /// Reversed ranges (min > max in source) are preserved to enable inversion. Constant ranges produce identical endpoints.
        /// When isImageMask is true the mapping is always materialized; identity is converted to reversed endpoints [255,0]
        /// and ascending ranges are inverted by transforming endpoints to (255 - min, 255 - max) which flips ordering.
        /// </summary>
        /// <param name="componentCount">Number of color components.</param>
        /// <param name="decodeArray">Source /Decode float array or null.</param>
        /// <param name="isImageMask">True when building decode for an image mask (single component, inverted semantics).</param>
        /// <returns>Byte array length componentCount*2 or null when identity and not an image mask.</returns>
        public static byte[] BuildDecodeMinSpanBytes(int componentCount, IReadOnlyList<float> decodeArray, bool isImageMask)
        {
            if (componentCount <= 0)
            {
                return null;
            }

            int required = componentCount * 2;
            bool valid = decodeArray != null && decodeArray.Count >= required;

            byte[] result = new byte[required];
            bool anyNonIdentity = false;

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                float min = valid ? decodeArray[componentIndex * 2] : 0f;
                float max = valid ? decodeArray[componentIndex * 2 + 1] : 1f;

                // Clamp without swapping to preserve reversal semantics.
                if (min < 0f)
                {
                    min = 0f;
                }
                else if (min > 1f)
                {
                    min = 1f;
                }
                if (max < 0f)
                {
                    max = 0f;
                }
                else if (max > 1f)
                {
                    max = 1f;
                }

                byte minByte = (byte)(min * 255f + 0.5f);
                byte maxByte = (byte)(max * 255f + 0.5f);

                int baseIndex = componentIndex * 2;
                result[baseIndex] = minByte;
                result[baseIndex + 1] = maxByte;

                if (!(minByte == 0 && maxByte == 255))
                {
                    anyNonIdentity = true;
                }
            }

            if (!anyNonIdentity && !isImageMask)
            {
                // Identity and not an image mask => no decode applied.
                return null;
            }

            if (isImageMask)
            {
                // For image masks invert ascending or identity ranges. Reversed ranges already produce desired semantics.
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    int baseIndex = componentIndex * 2;
                    byte minByte = result[baseIndex];
                    byte maxByte = result[baseIndex + 1];
                    if (maxByte >= minByte)
                    {
                        // Invert endpoints to reverse ordering.
                        byte invMin = (byte)(255 - minByte);
                        byte invMax = (byte)(255 - maxByte);
                        result[baseIndex] = invMin;
                        result[baseIndex + 1] = invMax;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Normalize a raw /Mask array (color key mask) to ascending min/max pairs per component and scale them into the 8-bit domain.
        /// Scaling rules follow sample expansion logic:
        /// 1 bpc: value * 255
        /// 2 bpc: value * 85
        /// 4 bpc: value * 17
        /// 8 bpc: identity
        /// 16 bpc: (value + 128) >> 8 (rounded high byte)
        /// Reversed raw pairs are swapped before scaling so that resulting scaled pairs are always ascending.
        /// Returns null when input is invalid.
        /// </summary>
        /// <param name="componentCount">Number of components expected (1, 3 or 4).</param>
        /// <param name="rawMask">Flattened /Mask array containing [min,max] pairs per component in raw sample domain.</param>
        /// <param name="bitsPerComponent">Bits per component of the image samples (1,2,4,8,16).</param>
        /// <returns>Scaled ascending [min,max] pairs in 0..255 domain or null.</returns>
        internal static int[] BuildNormalizedMaskRawPairs(int componentCount, int[] rawMask, int bitsPerComponent)
        {
            if (componentCount <= 0)
            {
                return null;
            }
            if (rawMask == null || rawMask.Length < componentCount * 2)
            {
                return null;
            }

            int[] normalized = new int[componentCount * 2];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int baseIndex = componentIndex * 2;
                int minRaw = rawMask[baseIndex];
                int maxRaw = rawMask[baseIndex + 1];
                if (minRaw > maxRaw)
                {
                    int temp = minRaw;
                    minRaw = maxRaw;
                    maxRaw = temp;
                }

                // Scale raw domain to 8-bit domain.
                int scaledMin = ScaleRawToByte(minRaw, bitsPerComponent);
                int scaledMax = ScaleRawToByte(maxRaw, bitsPerComponent);

                if (scaledMin < 0)
                {
                    scaledMin = 0;
                }
                if (scaledMin > 255)
                {
                    scaledMin = 255;
                }
                if (scaledMax < 0)
                {
                    scaledMax = 0;
                }
                if (scaledMax > 255)
                {
                    scaledMax = 255;
                }

                normalized[baseIndex] = scaledMin;
                normalized[baseIndex + 1] = scaledMax;
            }

            return normalized;
        }

        /// <summary>
        /// Scale a raw sample code in its native bits-per-component domain into an 8-bit value (0..255).
        /// </summary>
        /// <param name="raw">Raw sample value.</param>
        /// <param name="bitsPerComponent">Bits per component.</param>
        /// <returns>Scaled 0..255 value.</returns>
        private static int ScaleRawToByte(int raw, int bitsPerComponent)
        {
            switch (bitsPerComponent)
            {
                case 16:
                {
                    // Rounded high byte consistent with expansion logic.
                    return (raw + 128) >> 8;
                }
                case 8:
                {
                    return raw;
                }
                case 4:
                {
                    return (raw & 0x0F) * 17;
                }
                case 2:
                {
                    return (raw & 0x03) * 85;
                }
                case 1:
                {
                    return (raw & 0x01) * 255;
                }
                default:
                {
                    // Fallback: attempt proportional scaling when unexpected bpc encountered.
                    if (bitsPerComponent > 0 && bitsPerComponent < 16)
                    {
                        int domainMax = (1 << bitsPerComponent) - 1;
                        return domainMax == 0 ? 0 : (raw * 255) / domainMax;
                    }
                    return 0;
                }
            }
        }
    }
}