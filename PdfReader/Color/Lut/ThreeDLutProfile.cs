using System.Numerics;

namespace PdfReader.Color.Lut;

/// <summary>
/// Provides precomputed stride and bounds information for addressing values in a
/// three-dimensional lookup table (3D LUT) arranged as an RGB lattice.
/// </summary>
internal sealed class ThreeDLutProfile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ThreeDLutProfile"/> class.
    /// </summary>
    /// <param name="gridSize">The number of samples per axis in the cubic LUT. Must be at least 2.</param>
    public ThreeDLutProfile(int gridSize)
    {
        GridSize = gridSize;
        GridSizeMinusOne = gridSize - 1;
        TripleStrideG = GridSize;
        TripleStrideR = GridSize * GridSize;
        TripleStrideRG = TripleStrideR + TripleStrideG;
        LatticeMax = new Vector3(GridSize - 2f);
        Strides = new Vector3(TripleStrideR, TripleStrideG, 1f);
    }

    /// <summary>
    /// Gets the number of lattice points on each axis of the cubic LUT.
    /// </summary>
    public int GridSize { get; }

    /// <summary>
    /// Grid size minus one.
    /// </summary>
    public int GridSizeMinusOne { get; }

    /// <summary>
    /// Gets the linear index increment to advance by one step along the G axis.
    /// </summary>
    public int TripleStrideG { get; }

    /// <summary>
    /// Gets the linear index increment to advance by one step along the R axis.
    /// </summary>
    public int TripleStrideR { get; }

    /// <summary>
    /// Gets the combined linear index increment for advancing one step along both R and G axes.
    /// </summary>
    public int TripleStrideRG { get; }

    /// <summary>
    /// Gets the maximum base lattice coordinate used for trilinear interpolation, equal to
    /// (GridSize - 2) on each axis. This ensures that accessing the +1 neighbor remains in-bounds.
    /// </summary>
    public Vector3 LatticeMax { get; }

    /// <summary>
    /// Gets the per-axis linear index strides as a vector (R, G, B) where B stride is 1.
    /// Use as: linearIndex = r * RStride + g * GStride + b.
    /// </summary>
    public Vector3 Strides { get; }
}
