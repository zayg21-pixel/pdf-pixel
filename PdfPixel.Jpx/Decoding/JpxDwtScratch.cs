using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Reusable working buffers for the inverse wavelet transform.
/// </summary>
internal sealed class JpxDwtScratch
{
    /// <summary>
    /// Number of columns the transform reconstructs together in one strip.
    /// </summary>
    public const int ColumnStripWidth = 32;

    private int[] _integers = [];
    private float[] _interleavedSamples = [];

    /// <summary>
    /// Buffer of at least <paramref name="length"/> integers, holding the row or column strip a
    /// reversible transform is reconstructing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int[] GetIntegers(int length)
    {
        if (_integers.Length < length)
        {
            _integers = new int[length];
        }

        return _integers;
    }

    /// <summary>
    /// Buffer of at least <paramref name="length"/> samples, holding the row or column strip an
    /// irreversible transform is reconstructing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float[] GetInterleavedSamples(int length)
    {
        if (_interleavedSamples.Length < length)
        {
            _interleavedSamples = new float[length];
        }

        return _interleavedSamples;
    }
}
