using PdfReader.Color.Icc.Model;
using System;
using System.Numerics;

namespace PdfReader.Color.Icc.Utilities;

/// <summary>
/// CLUT evaluation utilities for ICC A2B pipelines.
/// Provides linear (trilinear / multilinear) interpolation for uniform and mAB (per-dimension) grids.
/// </summary>
internal static class IccClutEvaluator
{
    /// <summary>
    /// Evaluate a uniform (lut8/lut16) CLUT with linear interpolation.
    /// The input vin length determines the dimensionality (typically 3 for RGB or 4 for CMYK).
    /// </summary>
    /// <param name="lut">Pipeline containing a uniform grid.</param>
    /// <param name="vin">Normalized input components (0..1).</param>
    /// <returns>Interpolated output channel values.</returns>
    public static float[] EvaluateClutLinear(IccLutPipeline lut, float[] vin)
    {
        if (lut == null)
        {
            throw new ArgumentNullException(nameof(lut));
        }
        if (vin == null)
        {
            throw new ArgumentNullException(nameof(vin));
        }

        int dimensionCount = vin.Length;
        if (dimensionCount <= 0)
        {
            return Array.Empty<float>();
        }

        // Build per-dimension grid array (uniform grid => same size)
        var gridPerDim = new int[dimensionCount];
        for (int i = 0; i < dimensionCount; i++)
        {
            gridPerDim[i] = lut.GridPoints;
        }

        return EvaluateClutLinearCore(lut.Clut, lut.OutChannels, gridPerDim, vin);
    }

    /// <summary>
    /// Evaluate an mAB (per-dimension grid) CLUT with linear interpolation.
    /// </summary>
    /// <param name="pipeline">mAB pipeline.</param>
    /// <param name="vin">Normalized input components (0..1).</param>
    /// <returns>Interpolated output channel values.</returns>
    public static float[] EvaluateClutLinearMab(IccLutPipeline pipeline, float[] vin)
    {
        if (pipeline == null)
        {
            throw new ArgumentNullException(nameof(pipeline));
        }
        if (vin == null)
        {
            throw new ArgumentNullException(nameof(vin));
        }

        int[] gridPerDim = pipeline.GridPointsPerDim;
        if (gridPerDim == null || gridPerDim.Length == 0)
        {
            // Fallback: treat as uniform using InChannels
            gridPerDim = new int[pipeline.InChannels];
            for (int i = 0; i < gridPerDim.Length; i++)
            {
                gridPerDim[i] = pipeline.GridPoints > 0 ? pipeline.GridPoints : 1;
            }
        }

        return EvaluateClutLinearCore(pipeline.Clut, pipeline.OutChannels, gridPerDim, vin);
    }

    /// <summary>
    /// General pipeline evaluation for profiles with any input channel count (e.g. 3 = RGB/Lab, 4 = CMYK).
    /// Applies input (A) curves, optional matrix (only when 3-channel), optional CLUT, middle (M) curves, and output (B) curves.
    /// Produces PCS values (XYZ or Lab) without black point compensation or final color space conversion.
    /// Handles the case where the CLUT stage is omitted (offsetClut == 0 in the tag) by skipping interpolation.
    /// </summary>
    public static float[] EvaluatePipelineToPcs(IccProfile profile, IccLutPipeline pipeline, ReadOnlySpan<float> input)
    {
        if (profile == null)
        {
            return null;
        }
        if (pipeline == null)
        {
            return null;
        }

        int inChannels = pipeline.InChannels;
        if (inChannels <= 0)
        {
            return null;
        }

        int usable = Math.Min(inChannels, input.Length);
        float[] working = new float[usable];
        IccLutPipelineCache cache = pipeline.Cache;

        // Stage A (input curves)
        if (cache.InputTrcLuts != null && cache.InputTrcLuts.Length >= usable)
        {
            for (int i = 0; i < usable; i++)
            {
                working[i] = ColorMath.LookupLinear(cache.InputTrcLuts[i], input[i]);
            }
        }
        else
        {
            for (int i = 0; i < usable; i++)
            {
                working[i] = input[i];
            }
        }

        // Optional Matrix (only meaningful / allowed for 3 channels)
        if (inChannels == 3 && cache.MatrixRow0.HasValue && cache.MatrixRow1.HasValue && cache.MatrixRow2.HasValue)
        {
            Vector3 vec = new Vector3(working[0], working[1], working[2]);
            float X = Vector3.Dot(cache.MatrixRow0.Value, vec);
            float Y = Vector3.Dot(cache.MatrixRow1.Value, vec);
            float Z = Vector3.Dot(cache.MatrixRow2.Value, vec);
            if (cache.MatrixOffset.HasValue)
            {
                X += cache.MatrixOffset.Value.X;
                Y += cache.MatrixOffset.Value.Y;
                Z += cache.MatrixOffset.Value.Z;
            }
            working[0] = X;
            if (usable > 1)
            {
                working[1] = Y;
            }
            if (usable > 2)
            {
                working[2] = Z;
            }
        }

        bool hasClut = pipeline.Clut != null && pipeline.Clut.Length > 0;
        float[] clutOut;

        if (hasClut)
        {
            // Ensure input array for CLUT matches channel count expected.
            float[] clutInput;
            if (usable == working.Length)
            {
                clutInput = working;
            }
            else
            {
                clutInput = new float[inChannels];
                Array.Copy(working, clutInput, usable);
            }

            clutOut = pipeline.IsMab ? EvaluateClutLinearMab(pipeline, clutInput) : EvaluateClutLinear(pipeline, clutInput);
            if (clutOut == null)
            {
                return null;
            }
        }
        else
        {
            // No CLUT stage: propagate current working values forward.
            // If channel counts differ (rare without a CLUT), we will pad/truncate when applying curves.
            clutOut = new float[working.Length];
            Array.Copy(working, clutOut, working.Length);
        }

        // Middle (M) curves (only for mAB and only if present). Applied even if no CLUT existed.
        if (pipeline.IsMab && cache.MidTrcLuts != null)
        {
            int limit = Math.Min(clutOut.Length, cache.MidTrcLuts.Length);
            for (int i = 0; i < limit; i++)
            {
                clutOut[i] = ColorMath.LookupLinear(cache.MidTrcLuts[i], clutOut[i]);
            }
        }

        // Output (B) curves. Always produce an array sized to outputChannels.
        if (cache.OutputTrcLuts != null)
        {
            int outputChannels = Math.Max(pipeline.OutChannels, cache.OutputTrcLuts.Length);
            float[] pcs = new float[outputChannels];
            int limit = Math.Min(clutOut.Length, cache.OutputTrcLuts.Length);
            for (int i = 0; i < limit; i++)
            {
                pcs[i] = ColorMath.LookupLinear(cache.OutputTrcLuts[i], clutOut[i]);
            }
            // Remaining channels (if any) default to 0.
            return pcs;
        }

        // If no B curves LUTs, return CLUT (or working) output directly.
        return clutOut;
    }

    /// <summary>
    /// Core multilinear interpolation shared by uniform and per-dimension CLUTs.
    /// </summary>
    /// <param name="clut">Flattened CLUT array (output-major stride at innermost dimension).</param>
    /// <param name="outChannels">Number of output channels.</param>
    /// <param name="gridPointsPerDimension">Grid point counts for each input dimension.</param>
    /// <param name="vin">Normalized input components (0..1).</param>
    /// <returns>Interpolated output values.</returns>
    private static float[] EvaluateClutLinearCore(float[] clut, int outChannels, int[] gridPointsPerDimension, float[] vin)
    {
        int dimensionCount = gridPointsPerDimension.Length;
        if (dimensionCount != vin.Length)
        {
            // Clamp to the minimum safe dimension count
            dimensionCount = Math.Min(dimensionCount, vin.Length);
        }

        // Pre-allocate index and fraction arrays (stack alloc would need spans; keep simple per rules)
        var index0 = new int[dimensionCount];
        var fraction = new float[dimensionCount];

        // Clamp, scale and decompose positions
        for (int d = 0; d < dimensionCount; d++)
        {
            int grid = gridPointsPerDimension[d];
            if (grid <= 1)
            {
                index0[d] = 0;
                fraction[d] = 0f;
                continue;
            }

            float scale = grid - 1;
            float p = vin[d] * scale;
            if (p < 0f)
            {
                p = 0f;
            }
            else if (p > scale)
            {
                p = scale;
            }

            int i0 = (int)p; // floor since p >= 0
            float f = p - i0;
            index0[d] = i0;
            fraction[d] = f;
        }

        // Compute strides per dimension (innermost dimension is last index in loops => reverse order)
        var stride = new int[dimensionCount];
        int cumulative = outChannels;
        for (int d = dimensionCount - 1; d >= 0; d--)
        {
            stride[d] = cumulative;
            int grid = gridPointsPerDimension[d];
            cumulative *= grid;
        }

        var result = new float[outChannels];
        int vertexCount = 1 << dimensionCount;

        for (int vertexMask = 0; vertexMask < vertexCount; vertexMask++)
        {
            float weight = 1f;
            int offset = 0;

            for (int d = 0; d < dimensionCount; d++)
            {
                int grid = gridPointsPerDimension[d];
                int bit = vertexMask >> d & 1;
                int idx = index0[d] + bit;
                if (idx >= grid)
                {
                    weight = 0f;
                    break;
                }

                float f = fraction[d];
                weight *= bit == 0 ? 1f - f : f;
                offset += idx * stride[d];
            }

            if (weight == 0f)
            {
                continue;
            }

            int baseIndex = offset;
            for (int c = 0; c < outChannels; c++)
            {
                result[c] += clut[baseIndex + c] * weight;
            }
        }

        return result;
    }
}
