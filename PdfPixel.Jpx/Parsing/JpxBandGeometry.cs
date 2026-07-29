using PdfPixel.Jpx.Model;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Coordinate arithmetic for the resolution and subband grids of a tile, per ITU-T T.800 B.5.
/// </summary>
/// <remarks>
/// Every grid in JPEG 2000 is anchored at the origin of the reference grid, not at the origin
/// of the tile. A tile that does not start on a multiple of the relevant power of two therefore
/// has subbands whose first row and column are only partially covered by the leading code-block
/// of the partition. Deriving these bounds from a tile's width alone is only correct for a tile
/// at the origin, so every caller works from the tile's absolute coordinates instead.
/// </remarks>
internal static class JpxBandGeometry
{
    // TODO: [HIGH] Take the component's subsampling into account. Per ITU-T T.800 B.5 every grid
    // below the tile is derived from the tile-component bounds, tcx0 = ceil(tx0 / XRsiz), but
    // these helpers are handed reference-grid coordinates directly and so compute the geometry of
    // a component with XRsiz or YRsiz above 1 as though it were sampled at the full rate. That
    // gives its subbands the wrong bounds and its samples decode to noise. Fixing it means
    // threading the component through here, through JpxPrecinctState and JpxPacketHeaderParser,
    // sizing each component's tile buffer to its own sample count, and stretching the samples
    // back over the reference grid in JpxTileToRowConverter.
    // Covered by the failing baboon-subsample-uniform and baboon-subsample-chroma corpus files.

    /// <summary>
    /// Returns the start of the resolution grid containing the reference-grid coordinate
    /// <paramref name="tileCoordinate"/> reduced by <paramref name="levels"/> decomposition levels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetResolutionCoordinate(int tileCoordinate, int levels) => CeilShift(tileCoordinate, levels);

    /// <summary>
    /// Returns the horizontal band offset of a subband: 1 for the bands that carry the
    /// horizontal high-pass (HL and HH), 0 otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBandOffsetX(int resolution, JpxSubbandType subbandType)
        => (resolution == 0 || subbandType == JpxSubbandType.LH) ? 0 : 1;

    /// <summary>
    /// Returns the vertical band offset of a subband: 1 for the bands that carry the
    /// vertical high-pass (LH and HH), 0 otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBandOffsetY(int resolution, JpxSubbandType subbandType)
        => (resolution == 0 || subbandType == JpxSubbandType.HL) ? 0 : 1;

    /// <summary>
    /// Returns the number of decomposition levels a subband's coefficients are reduced by.
    /// The lowest resolution holds the residual LL band at the full decomposition depth;
    /// every other resolution holds detail bands one level coarser than the resolution below it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBandLevel(int resolution, int decompositionLevels)
        => (resolution == 0) ? decompositionLevels : (decompositionLevels - resolution + 1);

    /// <summary>
    /// Maps a reference-grid coordinate onto a subband's own coordinate grid.
    /// </summary>
    /// <param name="tileCoordinate">Reference-grid coordinate of the tile edge.</param>
    /// <param name="bandLevel">Decomposition level of the subband, from <see cref="GetBandLevel"/>.</param>
    /// <param name="bandOffset">Band offset along this axis, 0 or 1.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBandCoordinate(int tileCoordinate, int bandLevel, int bandOffset)
    {
        if (bandLevel == 0)
        {
            return tileCoordinate;
        }

        // tbx = ceil((tileCoordinate - 2^(bandLevel-1) * bandOffset) / 2^bandLevel)
        int shifted = tileCoordinate - (bandOffset << (bandLevel - 1));
        return CeilShift(shifted, bandLevel);
    }

    /// <summary>
    /// Divides by 2^<paramref name="shift"/>, rounding towards positive infinity.
    /// Handles the negative numerator produced by a band offset at the reference-grid origin.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CeilShift(int value, int shift)
    {
        if (shift <= 0)
        {
            return value;
        }

        int divisor = 1 << shift;

        if (value >= 0)
        {
            return (value + divisor - 1) >> shift;
        }

        return -((-value) >> shift);
    }
}
