using System.Runtime.InteropServices;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Pre-computed per-group metadata optimized for the sliding window decoder.
/// Stores the bit count, context shift, row offset, and the dx of the rightmost pixel
/// (the "load edge") so that each pixel step only needs to load one new bit.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct Jbig2SlidingWindowGroup
{
    /// <summary>
    /// Row offset relative to current pixel row (negative = above).
    /// </summary>
    internal readonly sbyte Dy;

    /// <summary>
    /// Number of bits in this group (MaxDx - MinDx + 1).
    /// </summary>
    internal readonly byte BitCount;

    /// <summary>
    /// Lowest context bit position for this group (same as <see cref="Jbig2RowGroupInfo.ContextShift"/>).
    /// </summary>
    internal readonly byte ContextShift;

    /// <summary>
    /// The dx offset of the rightmost pixel in this group (the trailing edge that enters the window).
    /// </summary>
    internal readonly sbyte LoadEdgeDx;

    /// <summary>
    /// Mask with <see cref="BitCount"/> lowest bits set, used to clamp the window before shifting into context.
    /// </summary>
    internal readonly uint WindowMask;

    /// <summary>
    /// Initializes a sliding window group from the corresponding row group info.
    /// </summary>
    internal Jbig2SlidingWindowGroup(in Jbig2RowGroupInfo grp)
    {
        Dy = grp.Dy;
        BitCount = (byte)(grp.MaxDx - grp.MinDx + 1);
        ContextShift = grp.ContextShift;
        LoadEdgeDx = grp.MaxDx;
        WindowMask = (1u << BitCount) - 1;
    }
}
