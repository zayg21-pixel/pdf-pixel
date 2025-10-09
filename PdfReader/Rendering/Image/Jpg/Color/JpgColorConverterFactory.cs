using System;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Decoding;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// Factory for selecting an appropriate JPEG color converter implementation based on header metadata.
    /// Adds heuristic detection for files that are already RGB (component Ids 'R','G','B') to avoid redundant YCbCr transform.
    /// </summary>
    internal static class JpgColorConverterFactory
    {
        public static IJpgColorConverter Create(JpgHeader header, JpgDecodingParameters parameters)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            // Single component (grayscale) requires no transform.
            if (header.ComponentCount == 1)
            {
                return new ColorClampConverter();
            }

            // Three components: Distinguish between true RGB and YCbCr.
            if (header.ComponentCount == 3)
            {
                if (LikelyRgb(header))
                {
                    // Already RGB; no in-place transform required.
                    return new ColorClampConverter();
                }
                return new YcbCrFloatColorConverter(header, parameters);
            }

            // Four components: If Adobe APP14 transform = 2 this is YCCK needing conversion to CMYK.
            if (header.ComponentCount == 4 && header.HasAdobeApp14 && header.AdobeColorTransform == 2)
            {
                return new YcckFloatColorConverter(header, parameters);
            }

            // Four components without YCCK transform: treat as native CMYK (no-op).
            if (header.ComponentCount == 4)
            {
                return new ColorClampConverter();
            }

            return new ColorClampConverter();
        }

        /// <summary>
        /// Heuristic detection for already RGB triplets. Returns true when component identifiers match
        /// ASCII 'R','G','B' (case-insensitive). Other common JPEG encodings use numeric ids 1,2,3 for Y,Cb,Cr.
        /// </summary>
        private static bool LikelyRgb(JpgHeader header)
        {
            if (header == null)
            {
                return false;
            }
            if (header.Components == null || header.Components.Count != 3)
            {
                return false;
            }

            // Accept both uppercase and lowercase to be tolerant.
            byte rId = header.Components[0].Id;
            byte gId = header.Components[1].Id;
            byte bId = header.Components[2].Id;

            bool isR = rId == (byte)'R' || rId == (byte)'r';
            bool isG = gId == (byte)'G' || gId == (byte)'g';
            bool isB = bId == (byte)'B' || bId == (byte)'b';

            if (isR && isG && isB)
            {
                return true;
            }

            return false;
        }

        private sealed class ColorClampConverter : IJpgColorConverter
        {
            public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
            {
                for (int i = 0; i < upsampledBandBlocks.Length; i++)
                {
                    Block8x8F[] bandBlocks = upsampledBandBlocks[i];
                    for (int j = 0; j < bandBlocks.Length; j++)
                    {
                        ref Block8x8F block = ref bandBlocks[j];
                        block.ClampToByte();
                    }
                }
            }
        }
    }
}
