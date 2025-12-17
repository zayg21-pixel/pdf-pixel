using PdfReader.Color.Icc.Transform;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Represents a parsed ICC LUT pipeline (AToB style) consisting of optional curve, matrix and CLUT stages.
/// Supports classic lut8/lut16 (uniform grids with input/output tables) and multi-process element (mAB) pipelines
/// with per-dimension grids, A/M/B curve sections, optional matrix + offset, and variable precision CLUT data.
/// This class is a lightweight container; heavy evaluation helpers live elsewhere.
/// </summary>
internal sealed class IccLutPipeline
{
    private readonly Lazy<IIccTransform> _lazyTransform;

    /// <summary>
    /// Create a uniform-grid (lut8 / lut16) pipeline.
    /// Input and output tables are assumed to be already normalized (0..1).
    /// </summary>
    public IccLutPipeline(int inCh, int outCh, int[] gridPointsPerDim, float[][] inTables, float[] clut, float[][] outTables, float[,] matrix3x3)
    {
        InChannels = inCh;
        OutChannels = outCh;
        GridPointsPerDim = gridPointsPerDim;
        InputTables = inTables;
        Clut = clut;
        OutputTables = outTables;
        Matrix3x3 = matrix3x3;
        IsMab = false;
        _lazyTransform = new Lazy<IIccTransform>(CreateTransform);
    }

    /// <summary>
    /// Number of input (device) channels.
    /// </summary>
    public int InChannels { get; }

    /// <summary>
    /// Number of output (PCS) channels.
    /// </summary>
    public int OutChannels { get; }

    /// <summary>
    /// Per-dimension grid point counts.
    /// </summary>
    public int[] GridPointsPerDim { get; set; }

    /// <summary>
    /// Input tables (lut8/lut16) holding channel-wise normalized samples.
    /// Null for mAB pipelines where A curves are stored separately.
    /// </summary>
    public float[][] InputTables { get; }

    /// <summary>
    /// Flattened CLUT sample data (interleaved output channels) or null when no CLUT stage is present.
    /// </summary>
    public float[] Clut { get; }

    /// <summary>
    /// Output tables (lut8/lut16) holding channel-wise normalized samples.
    /// Null for mAB pipelines where B curves are stored separately.
    /// </summary>
    public float[][] OutputTables { get; }

    /// <summary>
    /// True when this pipeline represents a multi-process element (mAB) sequence.
    /// </summary>
    public bool IsMab { get; private set; }

    /// <summary>
    /// A curves (one per input channel) for mAB pipelines. Null for legacy pipelines.
    /// </summary>
    public IccTrc[] CurvesA { get; private set; }

    /// <summary>
    /// M (middle) curves (one per intermediate/output channel) for mAB pipelines.
    /// </summary>
    public IccTrc[] CurvesM { get; private set; }

    /// <summary>
    /// B (output) curves (one per output channel) for mAB pipelines.
    /// </summary>
    public IccTrc[] CurvesB { get; private set; }

    /// <summary>
    /// Optional 3x3 matrix (s15Fixed16 expanded to float) used in lut8/lut16 and mAB pipelines when input channels == 3.
    /// </summary>
    public float[,] Matrix3x3 { get; set; }

    /// <summary>
    /// Optional matrix offset vector (length 3) used only in mAB pipelines when present in the tag.
    /// </summary>
    public float[] MatrixOffset { get; private set; }

    /// <summary>
    /// ICC transform representing this pipeline (lazily created).
    /// </summary>
    public IIccTransform Transform => _lazyTransform.Value;

    /// <summary>
    /// Factory for a multi-process element (mAB) pipeline.
    /// </summary>
    public static IccLutPipeline CreateMab(int inCh, int outCh, int[] gridPerDim, IccTrc[] a, IccTrc[] m, IccTrc[] b, float[] clut, float[,] matrix, float[] offset)
    {
        var pipeline = new IccLutPipeline(inCh, outCh, gridPerDim, null, clut, null, matrix)
        {
            CurvesA = a,
            CurvesM = m,
            CurvesB = b,
            Matrix3x3 = matrix,
            MatrixOffset = offset,
            IsMab = true
        };
        return pipeline;
    }

    private IIccTransform CreateTransform()
    {
        List<IIccTransform> transforms = new List<IIccTransform>();

        if (IsMab)
        {
            if (CurvesA != null)
            {
                transforms.Add(new IccPerChannelLutTransform(CurvesA));
            }

            if (Clut != null)
            {
                transforms.Add(new IccClutTransform(Clut, OutChannels, GridPointsPerDim));
            }

            if (CurvesM != null)
            {
                transforms.Add(new IccPerChannelLutTransform(CurvesM));
            }

            if (Matrix3x3 != null)
            {
                transforms.Add(new IccMatrixTransform(Matrix3x3, MatrixOffset));
            }

            if (CurvesB != null)
            {
                transforms.Add(new IccPerChannelLutTransform(CurvesB));
            }
        }
        else
        {
            if (InputTables != null)
            {
                transforms.Add(new IccPerChannelLutTransform(InputTables));
            }

            if (Matrix3x3 != null)
            {
                var matrix = IccVectorUtilities.ToMatrix4x4(Matrix3x3);
                matrix = Matrix4x4.Transpose(matrix);
                transforms.Add(new IccMatrixTransform(matrix));
            }

            if (Clut != null)
            {
                transforms.Add(new IccClutTransform(Clut, OutChannels, GridPointsPerDim));
            }

            if (OutputTables != null)
            {
                transforms.Add(new IccPerChannelLutTransform(OutputTables));
            }
        }

        return new IccChainedTransform(transforms.ToArray());
    }
}
