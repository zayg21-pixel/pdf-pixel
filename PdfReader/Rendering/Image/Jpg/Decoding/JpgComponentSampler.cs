using PdfReader.Rendering.Image.Jpg.Model;
using System;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Handles JPEG component sampling factor calculations and component plane sizing.
    /// Responsible for determining component dimensions based on sampling factors.
    /// </summary>
    internal sealed partial class JpgComponentSampler
    {
        /// <summary>
        /// Calculate sampling information for all components in the JPEG header.
        /// </summary>
        [Obsolete("Use streaming decode path (JpgBaselineStream) and per-MCU upsampling instead of plane-based sampling info." )]
        public static SamplingInfo CalculateSamplingInfo(JpgHeader header)
        {
            if (header == null || header.Components.Count == 0)
            {
                throw new ArgumentException("Invalid header or no components", nameof(header));
            }

            int maxHorizontalSampling = 1;
            int maxVerticalSampling = 1;

            // Find maximum sampling factors
            for (int componentIndex = 0; componentIndex < header.Components.Count; componentIndex++)
            {
                var component = header.Components[componentIndex];
                if (component.HorizontalSamplingFactor > maxHorizontalSampling)
                {
                    maxHorizontalSampling = component.HorizontalSamplingFactor;
                }

                if (component.VerticalSamplingFactor > maxVerticalSampling)
                {
                    maxVerticalSampling = component.VerticalSamplingFactor;
                }
            }

            int componentCount = header.ComponentCount;
            int[] componentWidths = new int[componentCount];
            int[] componentHeights = new int[componentCount];
            int[] componentBlocksX = new int[componentCount];
            int[] componentBlocksY = new int[componentCount];

            // Calculate component dimensions
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                var component = header.Components[componentIndex];
                int horizontalSample = component.HorizontalSamplingFactor;
                int verticalSample = component.VerticalSamplingFactor;

                componentWidths[componentIndex] = (header.Width * horizontalSample + maxHorizontalSampling - 1) / maxHorizontalSampling;
                componentHeights[componentIndex] = (header.Height * verticalSample + maxVerticalSampling - 1) / maxVerticalSampling;
                componentBlocksX[componentIndex] = (componentWidths[componentIndex] + 7) / 8;
                componentBlocksY[componentIndex] = (componentHeights[componentIndex] + 7) / 8;
            }

            return new SamplingInfo(
                maxHorizontalSampling,
                maxVerticalSampling,
                componentWidths,
                componentHeights,
                componentBlocksX,
                componentBlocksY);
        }

        /// <summary>
        /// Calculate MCU (Minimum Coded Unit) dimensions based on maximum sampling factors.
        /// </summary>
        [Obsolete("Use JpgBaselineStream.McuWidth/McuHeight and computed grid instead.")]
        public static (int mcuColumns, int mcuRows) CalculateMcuDimensions(JpgHeader header, SamplingInfo samplingInfo)
        {
            int mcuColumns = (header.Width + (8 * samplingInfo.MaxHorizontalSampling - 1)) / (8 * samplingInfo.MaxHorizontalSampling);
            int mcuRows = (header.Height + (8 * samplingInfo.MaxVerticalSampling - 1)) / (8 * samplingInfo.MaxVerticalSampling);
            return (mcuColumns, mcuRows);
        }

        /// <summary>
        /// Upsample a component plane to full image resolution using nearest neighbor.
        /// </summary>
        [Obsolete("Use per-MCU upsampling in the streaming pipeline instead of full-plane upsampling.")]
        public static byte[] UpsampleComponentToFullResolution(byte[] componentPlane, int componentWidth, int componentHeight, int fullWidth, int fullHeight)
        {
            if (componentWidth == fullWidth && componentHeight == fullHeight)
            {
                return componentPlane;
            }

            var upsampled = new byte[fullWidth * fullHeight];

            if (componentWidth <= 0 || componentHeight <= 0 || fullWidth <= 0 || fullHeight <= 0)
            {
                return upsampled;
            }

            for (int y = 0; y < fullHeight; y++)
            {
                double gy = (y + 0.5) * componentHeight / fullHeight - 0.5;
                int sy = (int)Math.Floor(gy);
                if (sy < 0)
                {
                    sy = 0;
                }
                else if (sy >= componentHeight)
                {
                    sy = componentHeight - 1;
                }

                int srcRow = sy * componentWidth;
                int dstRow = y * fullWidth;

                for (int x = 0; x < fullWidth; x++)
                {
                    double gx = (x + 0.5) * componentWidth / fullWidth - 0.5;
                    int sx = (int)Math.Floor(gx);
                    if (sx < 0)
                    {
                        sx = 0;
                    }
                    else if (sx >= componentWidth)
                    {
                        sx = componentWidth - 1;
                    }

                    upsampled[dstRow + x] = componentPlane[srcRow + sx];
                }
            }

            return upsampled;
        }
    }
}