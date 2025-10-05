using System;
using System.Numerics;

namespace PdfReader.Icc
{
    /// <summary>
    /// Immutable cache for a parsed LUT pipeline containing only precomputed data required at evaluation time.
    /// Stores expanded TRC lookup tables (converted to dense float samples) and lazily extracted matrix rows.
    /// This avoids recomputing TRC samples or matrix row vectors for every color conversion.
    /// </summary>
    internal sealed class IccLutPipelineCache
    {
        /// <summary>
        /// Construct a new cache instance.
        /// </summary>
        /// <param name="inputTrcLuts">Expanded A/input curve TRC LUTs (one per input channel) or null.</param>
        /// <param name="midTrcLuts">Expanded M/middle curve TRC LUTs (mAB only) or null.</param>
        /// <param name="outputTrcLuts">Expanded B/output curve TRC LUTs (one per output channel) or null.</param>
        /// <param name="matrixRow0">Optional first row of the 3x3 matrix.</param>
        /// <param name="matrixRow1">Optional second row of the 3x3 matrix.</param>
        /// <param name="matrixRow2">Optional third row of the 3x3 matrix.</param>
        /// <param name="matrixOffset">Optional matrix offset vector (length 3) for mAB pipelines.</param>
        public IccLutPipelineCache(
            float[][] inputTrcLuts,
            float[][] midTrcLuts,
            float[][] outputTrcLuts,
            Vector3? matrixRow0,
            Vector3? matrixRow1,
            Vector3? matrixRow2,
            Vector3? matrixOffset)
        {
            InputTrcLuts = inputTrcLuts;
            MidTrcLuts = midTrcLuts;
            OutputTrcLuts = outputTrcLuts;
            MatrixRow0 = matrixRow0;
            MatrixRow1 = matrixRow1;
            MatrixRow2 = matrixRow2;
            MatrixOffset = matrixOffset;
        }

        /// <summary>
        /// Expanded A/input curve TRC LUTs (one table per input channel) or null when absent.
        /// </summary>
        public float[][] InputTrcLuts { get; }

        /// <summary>
        /// Expanded M/middle curve TRC LUTs (mAB only) or null when absent.
        /// </summary>
        public float[][] MidTrcLuts { get; }

        /// <summary>
        /// Expanded B/output curve TRC LUTs (one table per output channel) or null when absent.
        /// </summary>
        public float[][] OutputTrcLuts { get; }

        /// <summary>
        /// First row of 3x3 matrix (if present) used for device -> PCS linear transform.
        /// </summary>
        public Vector3? MatrixRow0 { get; }

        /// <summary>
        /// Second row of 3x3 matrix (if present).
        /// </summary>
        public Vector3? MatrixRow1 { get; }

        /// <summary>
        /// Third row of 3x3 matrix (if present).
        /// </summary>
        public Vector3? MatrixRow2 { get; }

        /// <summary>
        /// Matrix offset vector (if present) applied after matrix multiplication.
        /// </summary>
        public Vector3? MatrixOffset { get; }
    }
}
