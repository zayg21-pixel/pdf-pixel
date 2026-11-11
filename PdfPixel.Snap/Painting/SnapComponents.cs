using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Painting;

/// <summary>
/// Operations on the components of one color, read through a reference.
/// </summary>
internal static class SnapComponents
{
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public static bool AreEqual(ref float first, ref float second, int count)
    {
        for (int component = 0; component < count; component++)
        {
            if (Unsafe.Add(ref first, component) != Unsafe.Add(ref second, component))
            {
                return false;
            }
        }

        return true;
    }
}
