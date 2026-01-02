using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class IndexedSampler : IRgbaSampler
{
    private readonly Vector4[] _palette;
    private readonly int _highValue;

    public IndexedSampler(Vector4[] palette, int highValue)
    {
        _palette = palette;
        _highValue = highValue;
    }

    public bool IsDefault => false;

    public Vector4 Sample(ReadOnlySpan<float> source)
    {
        int idx = 0;
        if (source.Length > 0)
        {
            idx = ClampIndex(source[0]);
        }

        return _palette[idx];
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
