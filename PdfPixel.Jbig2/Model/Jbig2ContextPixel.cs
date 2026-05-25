using System.Runtime.InteropServices;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// A pixel position within a context template, with its pre-assigned bit shift.
/// Stored as a compact value type to keep template data in a single flat array.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal readonly struct Jbig2ContextPixel(sbyte dx, sbyte dy, byte shift)
{
    /// <summary>
    /// Horizontal offset from the current pixel.
    /// </summary>
    [FieldOffset(0)]
    internal readonly sbyte Dx = dx;

    /// <summary>
    /// Vertical offset from the current pixel.
    /// </summary>
    [FieldOffset(1)]
    internal readonly sbyte Dy = dy;

    /// <summary>
    /// Bit position in the context label.
    /// </summary>
    [FieldOffset(2)]
    internal readonly byte Shift = shift;
}
