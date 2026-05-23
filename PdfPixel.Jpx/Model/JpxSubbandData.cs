using System;

namespace PdfPixel.Jpx.Model;

/// <summary>
/// Holds subband coefficient arrays for all decomposition levels of a single component.
/// Provides structured access to the LL (lowest resolution) and detail (HL, LH, HH)
/// subbands at each decomposition level.
/// </summary>
internal sealed class JpxSubbandData
{
    /// <summary>Full reconstructed width of the component.</summary>
    public readonly int FullWidth;

    /// <summary>Full reconstructed height of the component.</summary>
    public readonly int FullHeight;

    /// <summary>Number of DWT decomposition levels.</summary>
    public readonly int Levels;

    /// <summary>LL subband (lowest resolution) coefficient array.</summary>
    public readonly int[] LL;

    /// <summary>Width of the LL subband.</summary>
    public readonly int LLWidth;

    /// <summary>Height of the LL subband.</summary>
    public readonly int LLHeight;

    // Detail subbands indexed by [level * 3 + subbandType]
    private readonly int[][] _subbands;
    private readonly int[] _widths;
    private readonly int[] _heights;

    /// <summary>
    /// Creates subband storage for a component with the given dimensions and decomposition depth.
    /// </summary>
    /// <param name="fullWidth">Full component width in pixels.</param>
    /// <param name="fullHeight">Full component height in pixels.</param>
    /// <param name="levels">Number of DWT decomposition levels.</param>
    public JpxSubbandData(int fullWidth, int fullHeight, int levels)
    {
        FullWidth = fullWidth;
        FullHeight = fullHeight;
        Levels = levels;

        // LL: dimensions = ceil(fullSize / 2^levels)
        LLWidth = CeilDiv(fullWidth, 1 << levels);
        LLHeight = CeilDiv(fullHeight, 1 << levels);
        LL = new int[LLWidth * LLHeight];

        _subbands = new int[levels * 3][];
        _widths = new int[levels * 3];
        _heights = new int[levels * 3];

        for (int level = 0; level < levels; level++)
        {
            int divisor = 1 << (level + 1);
            int parentWidth = CeilDiv(fullWidth, divisor / 2);
            int parentHeight = CeilDiv(fullHeight, divisor / 2);
            int childWidth = CeilDiv(fullWidth, divisor);
            int childHeight = CeilDiv(fullHeight, divisor);

            // HL: width = parentWidth - childWidth, height = childHeight
            int hlW = parentWidth - childWidth;
            int hlH = childHeight;
            // LH: width = childWidth, height = parentHeight - childHeight
            int lhW = childWidth;
            int lhH = parentHeight - childHeight;
            // HH: width = parentWidth - childWidth, height = parentHeight - childHeight
            int hhW = parentWidth - childWidth;
            int hhH = parentHeight - childHeight;

            SetSubband(level, JpxSubbandType.HL, Math.Max(hlW, 0), Math.Max(hlH, 0));
            SetSubband(level, JpxSubbandType.LH, Math.Max(lhW, 0), Math.Max(lhH, 0));
            SetSubband(level, JpxSubbandType.HH, Math.Max(hhW, 0), Math.Max(hhH, 0));
        }
    }

    /// <summary>
    /// Gets the coefficient array for a detail subband at the specified level.
    /// </summary>
    public int[] GetSubband(int level, JpxSubbandType type) => _subbands[level * 3 + (int)type];

    /// <summary>
    /// Gets the width of a detail subband at the specified level.
    /// </summary>
    public int GetWidth(int level, JpxSubbandType type) => _widths[level * 3 + (int)type];

    /// <summary>
    /// Gets the height of a detail subband at the specified level.
    /// </summary>
    public int GetHeight(int level, JpxSubbandType type) => _heights[level * 3 + (int)type];

    private void SetSubband(int level, JpxSubbandType type, int width, int height)
    {
        int idx = level * 3 + (int)type;
        _widths[idx] = width;
        _heights[idx] = height;
        _subbands[idx] = new int[width * height];
    }

    private static int CeilDiv(int a, int b) => (a + b - 1) / b;
}
