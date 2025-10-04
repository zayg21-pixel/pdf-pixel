using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// Factory for creating MCU writers based on color mode and JPEG header information.
    /// </summary>
    internal static class McuWriterFactory
    {
        /// <summary>
        /// Create an appropriate MCU writer for the given JPEG header and component data.
        /// </summary>
        /// <param name="header">JPEG header containing component information.</param>
        /// <param name="componentTiles">Per-component MCU tiles (reused per MCU).</param>
        /// <param name="tileWidths">Width of each component tile.</param>
        /// <param name="tileHeights">Height of each component tile.</param>
        /// <param name="hMax">Maximum horizontal sampling factor.</param>
        /// <param name="vMax">Maximum vertical sampling factor.</param>
        /// <param name="mcuWidth">MCU width in pixels.</param>
        /// <param name="imageWidth">Image width in pixels.</param>
        /// <param name="outputStride">Output stride in bytes per row (RGBA).</param>
        /// <returns>An MCU writer appropriate for the color mode.</returns>
        public static IMcuWriter Create(
            JpgHeader header,
            byte[][] componentTiles,
            int[] tileWidths,
            int[] tileHeights,
            int hMax,
            int vMax,
            int mcuWidth,
            int imageWidth,
            int outputStride)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (componentTiles == null)
            {
                throw new ArgumentNullException(nameof(componentTiles));
            }

            if (tileWidths == null)
            {
                throw new ArgumentNullException(nameof(tileWidths));
            }

            if (tileHeights == null)
            {
                throw new ArgumentNullException(nameof(tileHeights));
            }

            int componentCount = header.ComponentCount;

            if (componentCount == 1)
            {
                return new GrayMcuWriter(
                    header,
                    componentTiles,
                    tileWidths,
                    hMax,
                    vMax,
                    mcuWidth,
                    imageWidth,
                    outputStride);
            }
            if (componentCount == 3)
            {
                bool isYCbCr = IsLikelyYCbCr(header);
                if (isYCbCr)
                {
                    return new YCbCrMcuWriter(
                        header,
                        componentTiles,
                        tileWidths,
                        hMax,
                        vMax,
                        mcuWidth,
                        imageWidth,
                        outputStride);
                }
                return new RgbMcuWriter(
                    header,
                    componentTiles,
                    tileWidths,
                    hMax,
                    vMax,
                    mcuWidth,
                    imageWidth,
                    outputStride);
            }
            if (componentCount == 4)
            {
                if (header.HasAdobeApp14 && header.AdobeColorTransform == 2)
                {
                    return new YcckMcuWriter(
                        header,
                        componentTiles,
                        tileWidths,
                        hMax,
                        vMax,
                        mcuWidth,
                        imageWidth,
                        outputStride);
                }
                return new CmykMcuWriter(
                    header,
                    componentTiles,
                    tileWidths,
                    hMax,
                    vMax,
                    mcuWidth,
                    imageWidth,
                    outputStride);
            }

            // Fallback: treat as RGB (rare non-standard component counts)
            return new RgbMcuWriter(
                header,
                componentTiles,
                tileWidths,
                hMax,
                vMax,
                mcuWidth,
                imageWidth,
                outputStride);
        }

        /// <summary>
        /// Determine if a 3-component JPEG is most likely YCbCr rather than RGB.
        /// Heuristics (ordered by confidence):
        /// 1. Adobe APP14 color transform (1 = YCbCr, 0 = RGB).
        /// 2. Component IDs 'R','G','B' (or lowercase) => RGB.
        /// 3. Component IDs 1,2,3 (typical JFIF) => YCbCr.
        /// 4. Subsampling: first component has larger sampling factors than the others => YCbCr.
        /// 5. Quantization tables: first component uses a different table while the other two share a table => YCbCr.
        /// Default: YCbCr (most encoders output YCbCr when metadata omitted).
        /// </summary>
        private static bool IsLikelyYCbCr(JpgHeader header)
        {
            if (header == null || header.Components == null || header.Components.Count != 3)
            {
                return false;
            }

            // Adobe APP14 explicit signaling
            if (header.HasAdobeApp14)
            {
                if (header.AdobeColorTransform == 1)
                {
                    return true; // Explicit YCbCr
                }
                if (header.AdobeColorTransform == 0)
                {
                    return false; // Explicit RGB
                }
            }

            var c0 = header.Components[0];
            var c1 = header.Components[1];
            var c2 = header.Components[2];

            // Component IDs 'R','G','B' -> treat as RGB
            bool idsAreRgb = IsRgbId(c0.Id) && IsRgbId(c1.Id) && IsRgbId(c2.Id);
            if (idsAreRgb)
            {
                return false;
            }

            // JFIF style numeric IDs 1,2,3 strongly imply YCbCr
            if (c0.Id == 1 && c1.Id == 2 && c2.Id == 3)
            {
                return true;
            }

            // Subsampling pattern: first (luma) component at higher sampling than chroma => YCbCr
            bool firstHigherSampling =
                (c0.HorizontalSamplingFactor > c1.HorizontalSamplingFactor || c0.HorizontalSamplingFactor > c2.HorizontalSamplingFactor) ||
                (c0.VerticalSamplingFactor > c1.VerticalSamplingFactor || c0.VerticalSamplingFactor > c2.VerticalSamplingFactor);
            if (firstHigherSampling)
            {
                return true;
            }

            // Quantization table pattern: luma uses table 0, chroma share table 1
            bool chromaShareTable = c1.QuantizationTableId == c2.QuantizationTableId && c0.QuantizationTableId != c1.QuantizationTableId;
            if (chromaShareTable)
            {
                return true;
            }

            // Fallback default for 3-component JPEGs without explicit indicators is YCbCr.
            return true;
        }

        private static bool IsRgbId(byte id)
        {
            return id == (byte)'R' || id == (byte)'G' || id == (byte)'B' || id == (byte)'r' || id == (byte)'g' || id == (byte)'b';
        }
    }
}