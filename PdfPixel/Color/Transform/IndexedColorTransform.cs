using System.Numerics;
using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace PdfPixel.Color.Transform;

internal sealed class IndexedColorTransform : IColorTransform
{
    private readonly Vector4[] _palette;
    private readonly int _highValue;

    public IndexedColorTransform(Vector4[] palette, int highValue)
    {
        _palette = palette;
        _highValue = highValue;
    }

    public bool IsIdentity => false;

    public Vector4 Transform(Vector4 color) => LookupSample(_palette, (int)color.X, _highValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 LookupSample(Vector4[] samples, int index, int highValue)
    {
        if (index < 0)
        {
            index = 0;
        }

        if (index > highValue)
        {
            index = highValue;
        }

#if NETSTANDARD2_0
        return Unsafe.Add(ref samples[0], index);
#else
        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(samples), index);
#endif
    }
}
