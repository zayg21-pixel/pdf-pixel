using System;
using PdfReader.Rendering.Image.Jpg.Decoding;
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
            else if (componentCount == 3)
            {
                bool isYCbCr = header.IsJfif || header.IsExif || header.HasAdobeApp14 && header.AdobeColorTransform == 1;
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
                else
                {
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
            }
            else if (componentCount == 4)
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
                else
                {
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
            }
            else
            {
                // Default to RGB for unknown component counts
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
        }
    }
}