using PdfReader.Color.Structures;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

internal sealed class IndexedSampler : IRgbaSampler
{
    private readonly RgbaPacked[] _palette;
    private readonly int _highValue;

    public IndexedSampler(RgbaPacked[] palette, int highValue)
    {
        _palette = palette;
        _highValue = highValue;
    }

    public bool IsDefault => false;

    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        int idx = 0;
        if (source.Length > 0)
        {
            idx = ClampIndex(source[0]);
        }

        destination = _palette[idx];
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
