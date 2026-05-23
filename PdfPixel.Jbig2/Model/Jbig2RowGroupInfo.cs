using System.Runtime.InteropServices;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Metadata for a single row group (all template pixels sharing the same dy).
/// Within a row group the context bit shifts are always consecutive (ITU-T T.88 layout),
/// so the entire group can be extracted from a sliding window with one mask+shift.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct Jbig2RowGroupInfo
{
    /// <summary>Row offset relative to the current pixel row.</summary>
    internal readonly sbyte Dy;

    /// <summary>Minimum dx (leftmost pixel in this group).</summary>
    internal readonly sbyte MinDx;

    /// <summary>Maximum dx (rightmost pixel in this group).</summary>
    internal readonly sbyte MaxDx;

    /// <summary>Lowest context bit position for this group.</summary>
    internal readonly byte ContextShift;

    /// <summary>
    /// When set, this group reads from the reference bitmap (with refDx/refDy offsets applied)
    /// rather than the primary bitmap being decoded.
    /// </summary>
    internal readonly byte IsReference;

    /// <summary>
    /// Initializes a new row group descriptor.
    /// </summary>
    /// <param name="dy">Row offset relative to the current pixel row.</param>
    /// <param name="minDx">Minimum dx (leftmost pixel).</param>
    /// <param name="maxDx">Maximum dx (rightmost pixel).</param>
    /// <param name="contextShift">Lowest context bit position for this group.</param>
    /// <param name="isReference">Whether this group reads from the reference bitmap.</param>
    internal Jbig2RowGroupInfo(sbyte dy, sbyte minDx, sbyte maxDx, byte contextShift, bool isReference = false)
    {
        Dy = dy;
        MinDx = minDx;
        MaxDx = maxDx;
        ContextShift = contextShift;
        IsReference = isReference ? (byte)1 : (byte)0;
    }
}
