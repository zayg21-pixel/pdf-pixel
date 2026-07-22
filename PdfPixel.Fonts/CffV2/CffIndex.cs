namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Describes the location of a CFF INDEX structure within font data.
/// </summary>
internal readonly struct CffIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CffIndex"/> struct.
    /// </summary>
    /// <param name="count">The number of entries in the INDEX.</param>
    /// <param name="dataStart">The absolute position where the INDEX entry data begins.</param>
    /// <param name="offsets">The 1-based entry offsets (length <paramref name="count"/> + 1), relative to <paramref name="dataStart"/>.</param>
    /// <param name="endPosition">The absolute position immediately following the INDEX.</param>
    public CffIndex(int count, int dataStart, int[] offsets, int endPosition)
    {
        Count = count;
        DataStart = dataStart;
        Offsets = offsets;
        EndPosition = endPosition;
    }

    /// <summary>
    /// Gets the number of entries in the INDEX.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the absolute position where the INDEX entry data begins.
    /// </summary>
    public int DataStart { get; }

    /// <summary>
    /// Gets the 1-based entry offsets (length <see cref="Count"/> + 1), relative to <see cref="DataStart"/>.
    /// </summary>
    public int[] Offsets { get; }

    /// <summary>
    /// Gets the absolute position immediately following the INDEX.
    /// </summary>
    public int EndPosition { get; }
}
