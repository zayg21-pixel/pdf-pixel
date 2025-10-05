using System;
using System.Numerics;

namespace PdfReader.Icc
{
    /// <summary>
    /// Represents a parsed ICC LUT pipeline (AToB style) consisting of optional curve, matrix and CLUT stages.
    /// Supports classic lut8/lut16 (uniform grids with input/output tables) and multi-process element (mAB) pipelines
    /// with per-dimension grids, A/M/B curve sections, optional matrix + offset, and variable precision CLUT data.
    /// This class is a lightweight container; heavy evaluation helpers live elsewhere.
    /// </summary>
    internal sealed class IccLutPipeline
    {
        /// <summary>
        /// Create a uniform-grid (lut8 / lut16) pipeline.
        /// Input and output tables are assumed to be already normalized (0..1).
        /// </summary>
        public IccLutPipeline(int inCh, int outCh, int grid, float[][] inTables, float[] clut, float[][] outTables)
        {
            InChannels = inCh;
            OutChannels = outCh;
            GridPoints = grid;
            InputTables = inTables;
            Clut = clut;
            OutputTables = outTables;
            IsMab = false;
            _lazyCache = new Lazy<IccLutPipelineCache>(CreateCache, isThreadSafe: true);
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
        /// Uniform grid point count (used in legacy lut8/lut16 pipelines). Zero for mAB pipelines.
        /// </summary>
        public int GridPoints { get; }

        /// <summary>
        /// Per-dimension grid point counts (mAB pipelines). Null for uniform legacy pipelines.
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
        /// Bytes per CLUT sample (1 or 2) for mAB CLUTs; for uniform pipelines reflects source precision.
        /// </summary>
        public byte ClutPrecisionBytes { get; set; }

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

        private readonly Lazy<IccLutPipelineCache> _lazyCache;

        /// <summary>
        /// Lazily initialized cache (expanded TRC LUTs, matrix row vectors, offset). Access triggers construction on first use.
        /// </summary>
        public IccLutPipelineCache Cache => _lazyCache.Value;

        /// <summary>
        /// Factory for a multi-process element (mAB) pipeline.
        /// </summary>
        public static IccLutPipeline CreateMab(int inCh, int outCh, int[] gridPerDim, byte precisionBytes, IccTrc[] a, IccTrc[] m, IccTrc[] b, float[] clut, float[,] matrix, float[] offset)
        {
            var pipeline = new IccLutPipeline(inCh, outCh, 0, null, clut, null)
            {
                GridPointsPerDim = gridPerDim,
                ClutPrecisionBytes = precisionBytes,
                CurvesA = a,
                CurvesM = m,
                CurvesB = b,
                Matrix3x3 = matrix,
                MatrixOffset = offset,
                IsMab = true
            };
            return pipeline;
        }

        /// <summary>
        /// Build the immutable pipeline cache (invoked once via Lazy). Performs TRC expansion for mAB.
        /// </summary>
        private IccLutPipelineCache CreateCache()
        {
            const int trcLutSize = IccProfileHelpers.TrcLutSize;

            float[][] inputTrcLuts = null;
            float[][] midTrcLuts = null;
            float[][] outputTrcLuts = null;

            if (IsMab)
            {
                if (CurvesA != null)
                {
                    inputTrcLuts = new float[CurvesA.Length][];
                    for (int i = 0; i < CurvesA.Length; i++)
                    {
                        inputTrcLuts[i] = IccProfileHelpers.IccTrcToLut(CurvesA[i], trcLutSize);
                    }
                }
                if (CurvesM != null)
                {
                    midTrcLuts = new float[CurvesM.Length][];
                    for (int i = 0; i < CurvesM.Length; i++)
                    {
                        midTrcLuts[i] = IccProfileHelpers.IccTrcToLut(CurvesM[i], trcLutSize);
                    }
                }
                if (CurvesB != null)
                {
                    outputTrcLuts = new float[CurvesB.Length][];
                    for (int i = 0; i < CurvesB.Length; i++)
                    {
                        outputTrcLuts[i] = IccProfileHelpers.IccTrcToLut(CurvesB[i], trcLutSize);
                    }
                }
            }
            else
            {
                inputTrcLuts = InputTables;   // Pre-normalized
                outputTrcLuts = OutputTables; // Pre-normalized
            }

            Vector3? matrixRow0 = null;
            Vector3? matrixRow1 = null;
            Vector3? matrixRow2 = null;
            if (Matrix3x3 != null && Matrix3x3.Length >= 9)
            {
                matrixRow0 = new Vector3(Matrix3x3[0, 0], Matrix3x3[0, 1], Matrix3x3[0, 2]);
                matrixRow1 = new Vector3(Matrix3x3[1, 0], Matrix3x3[1, 1], Matrix3x3[1, 2]);
                matrixRow2 = new Vector3(Matrix3x3[2, 0], Matrix3x3[2, 1], Matrix3x3[2, 2]);
            }

            Vector3? offsetVector = null;
            if (MatrixOffset != null && MatrixOffset.Length >= 3)
            {
                offsetVector = new Vector3(MatrixOffset[0], MatrixOffset[1], MatrixOffset[2]);
            }

            return new IccLutPipelineCache(
                inputTrcLuts,
                midTrcLuts,
                outputTrcLuts,
                matrixRow0,
                matrixRow1,
                matrixRow2,
                offsetVector);
        }
    }
}
