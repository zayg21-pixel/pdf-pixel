namespace PdfReader.Icc
{
    /// <summary>
    /// Base structural metadata for ICC LUT (AToB / BToA) tags capturing channel counts, grid dimension
    /// and presence flags for individual pipeline stages (curves, matrix, CLUT). Does not contain raw
    /// table data; evaluation uses the parsed <see cref="IccLutPipeline"/> when available.
    /// </summary>
    internal class IccLutMeta
    {
        /// <summary>
        /// Create LUT structural metadata descriptor.
        /// </summary>
        /// <param name="inCh">Number of input (device) channels.</param>
        /// <param name="outCh">Number of output (PCS) channels.</param>
        /// <param name="gridPoints">Uniform grid point count (legacy lut8/lut16) or nominal value for mAB/mBA.</param>
        /// <param name="hasCurvesA">Input (A) curves present.</param>
        /// <param name="hasMatrix">Matrix/offset stage present (usually for 3-channel RGB).</param>
        /// <param name="hasCurvesM">Middle (M) curves present (mAB only).</param>
        /// <param name="hasClut">CLUT stage present.</param>
        /// <param name="hasCurvesB">Output (B) curves present.</param>
        public IccLutMeta(byte inCh, byte outCh, byte gridPoints, bool hasCurvesA, bool hasMatrix, bool hasCurvesM, bool hasClut, bool hasCurvesB)
        {
            InChannels = inCh;
            OutChannels = outCh;
            GridPoints = gridPoints;
            HasCurvesA = hasCurvesA;
            HasMatrix = hasMatrix;
            HasCurvesM = hasCurvesM;
            HasClut = hasClut;
            HasCurvesB = hasCurvesB;
        }

        /// <summary>
        /// Number of input (device) channels.
        /// </summary>
        public byte InChannels { get; }

        /// <summary>
        /// Number of output (PCS) channels.
        /// </summary>
        public byte OutChannels { get; }

        /// <summary>
        /// Uniform grid points (legacy lut8/lut16) or nominal grid value for multi-process elements.
        /// </summary>
        public byte GridPoints { get; }

        /// <summary>
        /// True if A (input) curves are present.
        /// </summary>
        public bool HasCurvesA { get; }

        /// <summary>
        /// True if a 3x3 matrix (+ optional offset) stage is present.
        /// </summary>
        public bool HasMatrix { get; }

        /// <summary>
        /// True if M (middle) curves are present (mAB/mBA only).
        /// </summary>
        public bool HasCurvesM { get; }

        /// <summary>
        /// True if a CLUT stage is present.
        /// </summary>
        public bool HasClut { get; }

        /// <summary>
        /// True if B (output) curves are present.
        /// </summary>
        public bool HasCurvesB { get; }

        /// <summary>
        /// Tag type signature string (e.g., "lut8", "mAB ").
        /// </summary>
        public string TagType { get; set; }

        /// <summary>
        /// True if this metadata describes an AToB (device->PCS) pipeline; false for BToA.
        /// </summary>
        public bool IsAToB { get; set; }
    }

    /// <summary>
    /// Structural metadata for an AToB (device to PCS) LUT tag.
    /// </summary>
    internal sealed class IccLutAToB : IccLutMeta
    {
        public IccLutAToB(byte inCh, byte outCh, byte gridPoints, bool hasCurvesA, bool hasMatrix, bool hasCurvesM, bool hasClut, bool hasCurvesB)
            : base(inCh, outCh, gridPoints, hasCurvesA, hasMatrix, hasCurvesM, hasClut, hasCurvesB)
        {
        }
    }

    /// <summary>
    /// Structural metadata for a BToA (PCS to device) LUT tag.
    /// </summary>
    internal sealed class IccLutBToA : IccLutMeta
    {
        public IccLutBToA(byte inCh, byte outCh, byte gridPoints, bool hasCurvesA, bool hasMatrix, bool hasCurvesM, bool hasClut, bool hasCurvesB)
            : base(inCh, outCh, gridPoints, hasCurvesA, hasMatrix, hasCurvesM, hasClut, hasCurvesB)
        {
        }
    }
}
