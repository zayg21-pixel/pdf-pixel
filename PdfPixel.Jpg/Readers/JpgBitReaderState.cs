namespace PdfPixel.Jpg.Readers;

/// <summary>
/// Serializable snapshot of a <see cref="JpgBitReader"/> internal state.
/// </summary>
internal readonly struct JpgBitReaderState
{
    /// <summary>
    /// Byte position (number of source bytes consumed).
    /// </summary>
    public readonly int Pos;

    /// <summary>
    /// Buffered bits reservoir.
    /// </summary>
    public readonly ulong BitBuf;

    /// <summary>
    /// Count of valid bits currently in <see cref="BitBuf"/>.
    /// </summary>
    public readonly int Bits;

    /// <summary>
    /// True if a marker prefix (0xFF) was encountered and pending marker consumption prevented further byte fetch.
    /// </summary>
    public readonly bool MarkerPending;

    /// <summary>
    /// Initialize a new snapshot instance.
    /// </summary>
    public JpgBitReaderState(int pos, ulong bitBuf, int bits, bool markerPending)
    {
        Pos = pos;
        BitBuf = bitBuf;
        Bits = bits;
        MarkerPending = markerPending;
    }
}
