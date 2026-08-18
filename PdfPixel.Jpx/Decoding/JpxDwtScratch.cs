namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Reusable working buffers for the inverse wavelet transform.
/// </summary>
/// <remarks>
/// The transform works in scratch the size of the resolution it reconstructs, which for an
/// image held in one tile is the whole component. One instance is shared by every tile and
/// component of an image, so that only one set of those buffers exists at a time; each grows
/// to fit the largest resolution seen and is reused from then on.
/// </remarks>
internal sealed class JpxDwtScratch
{
    private int[] _integers = [];
    private float[] _interleavedSamples = [];
    private float[] _lowpassSamples = [];

    /// <summary>
    /// Buffer of at least <paramref name="length"/> integers, holding the level a reversible
    /// transform is reconstructing.
    /// </summary>
    public int[] GetIntegers(int length)
    {
        if (_integers.Length < length)
        {
            _integers = new int[length];
        }

        return _integers;
    }

    /// <summary>
    /// Buffer of at least <paramref name="length"/> samples, holding the level an irreversible
    /// transform is reconstructing.
    /// </summary>
    public float[] GetInterleavedSamples(int length)
    {
        if (_interleavedSamples.Length < length)
        {
            _interleavedSamples = new float[length];
        }

        return _interleavedSamples;
    }

    /// <summary>
    /// Buffer of at least <paramref name="length"/> samples, holding the level an irreversible
    /// transform has already reconstructed.
    /// </summary>
    public float[] GetLowpassSamples(int length)
    {
        if (_lowpassSamples.Length < length)
        {
            _lowpassSamples = new float[length];
        }

        return _lowpassSamples;
    }
}
