using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using SkiaSharp;
using System;

namespace PdfReader.Color.Icc.Transform
{
    internal sealed partial class IccClutTransform
    {
        /// <summary>
        /// Builds an ICC CLUT (Color Look-Up Table) transform using the provided converter function.
        /// The CLUT is sampled uniformly over each input dimension and stores the converted results.
        /// </summary>
        /// <param name="intent">The rendering intent controlling color conversion.</param>
        /// <param name="converter">Delegate converting normalized input color to sRGB SKColor.</param>
        /// <param name="outChannels">Number of output channels (1-4 supported, typically 3 for RGB or 4 for CMYK).</param>
        /// <param name="gridPointsPerDimension">Array specifying grid points per input dimension.</param>
        /// <returns>A new <see cref="IccClutTransform"/> instance containing the sampled CLUT, or null if converter is null.</returns>
        public static IccClutTransform Build(PdfRenderingIntent intent, DeviceToSrgbCore converter, int outChannels, int[] gridPointsPerDimension)
        {
            if (converter == null)
            {
                return null;
            }

            if (gridPointsPerDimension == null || gridPointsPerDimension.Length == 0)
            {
                throw new ArgumentException("Grid points per dimension cannot be null or empty.", nameof(gridPointsPerDimension));
            }

            if (outChannels < 1 || outChannels > 4)
            {
                throw new ArgumentException("Output channels must be between 1 and 4.", nameof(outChannels));
            }

            // Calculate total number of grid points
            int totalGridPoints = 1;
            for (int i = 0; i < gridPointsPerDimension.Length; i++)
            {
                if (gridPointsPerDimension[i] <= 0)
                {
                    throw new ArgumentException($"Grid points must be positive. Invalid value at dimension {i}: {gridPointsPerDimension[i]}", nameof(gridPointsPerDimension));
                }
                totalGridPoints *= gridPointsPerDimension[i];
            }

            // Build CLUT by sampling the converter function
            float[] clut = new float[totalGridPoints * outChannels];
            float[] input = new float[gridPointsPerDimension.Length];

            int writeIndex = 0;
            SampleGridRecursive(gridPointsPerDimension, input, 0, converter, intent, outChannels, clut, ref writeIndex);

            return new IccClutTransform(clut, outChannels, gridPointsPerDimension);
        }

        /// <summary>
        /// Builds an ICC CLUT (Color Look-Up Table) transform using the provided converter function with uniform grid size.
        /// The CLUT is sampled uniformly over each input dimension using the same number of grid points for all dimensions.
        /// </summary>
        /// <param name="intent">The rendering intent controlling color conversion.</param>
        /// <param name="converter">Delegate converting normalized input color to sRGB SKColor.</param>
        /// <param name="outChannels">Number of output channels (1-4 supported, typically 3 for RGB or 4 for CMYK).</param>
        /// <param name="inputDimensions">Number of input dimensions (e.g., 3 for RGB, 4 for CMYK).</param>
        /// <param name="gridSize">Number of grid points per dimension (e.g., 16 for a 16x16x16 LUT).</param>
        /// <returns>A new <see cref="IccClutTransform"/> instance containing the sampled CLUT, or null if converter is null.</returns>
        public static IccClutTransform Build(PdfRenderingIntent intent, DeviceToSrgbCore converter, int outChannels, int inputDimensions, int gridSize = 16)
        {
            if (converter == null)
            {
                return null;
            }

            if (inputDimensions <= 0)
            {
                throw new ArgumentException("Input dimensions must be positive.", nameof(inputDimensions));
            }

            if (gridSize <= 0)
            {
                throw new ArgumentException("Grid size must be positive.", nameof(gridSize));
            }

            if (outChannels < 1 || outChannels > 4)
            {
                throw new ArgumentException("Output channels must be between 1 and 4.", nameof(outChannels));
            }

            // Create uniform grid points array
            int[] gridPointsPerDimension = new int[inputDimensions];
            for (int i = 0; i < inputDimensions; i++)
            {
                gridPointsPerDimension[i] = gridSize;
            }

            return Build(intent, converter, outChannels, gridPointsPerDimension);
        }

        /// <summary>
        /// Recursively samples the grid points for CLUT construction.
        /// </summary>
        private static void SampleGridRecursive(int[] gridPointsPerDimension, float[] input, int dimension, 
            DeviceToSrgbCore converter, PdfRenderingIntent intent, int outChannels, float[] clut, ref int writeIndex)
        {
            if (dimension >= gridPointsPerDimension.Length)
            {
                // Base case: convert the input and store the result
                SKColor color = converter(input, intent);
                
                // Store based on output channels
                switch (outChannels)
                {
                    case 1: // Grayscale
                        clut[writeIndex++] = color.Red / 255f; // Use red channel for gray
                        break;
                    case 2: // Two-channel (e.g., grayscale + alpha)
                        clut[writeIndex++] = color.Red / 255f;
                        clut[writeIndex++] = color.Alpha / 255f;
                        break;
                    case 3: // RGB
                        clut[writeIndex++] = color.Red / 255f;
                        clut[writeIndex++] = color.Green / 255f;
                        clut[writeIndex++] = color.Blue / 255f;
                        break;
                    case 4: // RGBA or CMYK
                        clut[writeIndex++] = color.Red / 255f;
                        clut[writeIndex++] = color.Green / 255f;
                        clut[writeIndex++] = color.Blue / 255f;
                        clut[writeIndex++] = color.Alpha / 255f;
                        break;
                }
                return;
            }

            // Recursive case: iterate through all grid points in the current dimension
            int gridPoints = gridPointsPerDimension[dimension];
            for (int i = 0; i < gridPoints; i++)
            {
                // Normalize grid position to [0, 1]
                input[dimension] = gridPoints > 1 ? (float)i / (gridPoints - 1) : 0f;
                
                // Recurse to the next dimension
                SampleGridRecursive(gridPointsPerDimension, input, dimension + 1, converter, intent, outChannels, clut, ref writeIndex);
            }
        }
    }
}