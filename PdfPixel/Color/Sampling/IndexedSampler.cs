using System.Numerics;
using System.Runtime.CompilerServices;

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

    // Receives Vector4 from ToVector4WithOnePadding; the index is in X.
    public Vector4 Transform(Vector4 color)
    {
        return _palette[ClampIndex(color.X)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ClampIndex(float rawIndex)
    {
        if (float.IsNaN(rawIndex))
        {
            return 0;
        }

        int idx = (int)rawIndex;

        if (idx < 0)
        {
            return 0;
        }

        if (idx > _highValue)
        {
            return _highValue;
        }

        return idx;
    }
}
