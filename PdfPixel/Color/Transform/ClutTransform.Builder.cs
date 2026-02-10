using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using System;
using System.Numerics;

namespace PdfPixel.Color.Icc.Transform;

internal sealed partial class ClutTransform
{
    /// <summary>
    /// Builds an ICC CLUT (Color Look-Up Table) transform using the provided converter function.
    /// The CLUT is sampled uniformly over each input dimension and stores the converted results.
    /// </summary>
    /// <param name="sampler">Sampler for converting input color to sRGB SKColor.</param>
    /// <param name="outChannels">Number of output channels (1-4 supported, typically 3 for RGB or 4 for CMYK).</param>
    /// <param name="gridPointsPerDimension">Array specifying grid points per input dimension.</param>
    /// <returns>A new <see cref="ClutTransform"/> instance containing the sampled CLUT, or null if sampler is null.</returns>
    public static ClutTransform Build(IRgbaSampler sampler, int outChannels, int[] gridPointsPerDimension)
    {
        if (sampler == null)
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
        SampleGridRecursive(gridPointsPerDimension, input, 0, sampler, outChannels, clut, ref writeIndex);

        return new ClutTransform(clut, outChannels, gridPointsPerDimension);
    }

    /// <summary>
    /// Builds an ICC CLUT (Color Look-Up Table) transform using the provided converter function with uniform grid size.
    /// The CLUT is sampled uniformly over each input dimension using the same number of grid points for all dimensions.
    /// </summary>
    /// <param name="sampler">Sampler for converting input color to sRGB SKColor.</param>
    /// <param name="outChannels">Number of output channels (1-4 supported, typically 3 for RGB or 4 for CMYK).</param>
    /// <param name="inputDimensions">Number of input dimensions (e.g., 3 for RGB, 4 for CMYK).</param>
    /// <param name="gridSize">Number of grid points per dimension (e.g., 16 for a 16x16x16 LUT).</param>
    /// <returns>A new <see cref="ClutTransform"/> instance containing the sampled CLUT, or null if sampler is null.</returns>
    public static ClutTransform Build(IRgbaSampler sampler, int outChannels, int inputDimensions, int gridSize = 16)
    {
        if (sampler == null)
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

        return Build(sampler, outChannels, gridPointsPerDimension);
    }

    /// <summary>
    /// Recursively samples the grid points for CLUT construction.
    /// </summary>
    private static void SampleGridRecursive(int[] gridPointsPerDimension, float[] input, int dimension, 
        IRgbaSampler sampler, int outChannels, float[] clut, ref int writeIndex)
    {
        if (dimension >= gridPointsPerDimension.Length)
        {
            // Base case: convert the input and store the result
            var color = Vector4.Clamp(sampler.Sample(input), Vector4.Zero, Vector4.One);

            // Store based on output channels
            switch (outChannels)
            {
                case 1: // Grayscale
                    clut[writeIndex++] = color.X; // Use red channel for gray
                    break;
                case 2: // Two-channel (e.g., grayscale + alpha)
                    clut[writeIndex++] = color.X;
                    clut[writeIndex++] = color.Y;
                    break;
                case 3: // RGB
                    clut[writeIndex++] = color.X;
                    clut[writeIndex++] = color.Y;
                    clut[writeIndex++] = color.Z;
                    break;
                case 4: // RGBA or CMYK
                    clut[writeIndex++] = color.X;
                    clut[writeIndex++] = color.Y;
                    clut[writeIndex++] = color.Z;
                    clut[writeIndex++] = color.W;
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
            SampleGridRecursive(gridPointsPerDimension, input, dimension + 1, sampler, outChannels, clut, ref writeIndex);
        }
    }
}