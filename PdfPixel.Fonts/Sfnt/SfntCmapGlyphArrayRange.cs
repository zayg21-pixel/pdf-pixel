namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A "cmap" range whose glyph id is read from an explicit array, indexed by <c>code - StartCode</c>,
/// then adjusted by <see cref="IdDelta"/> - covers format 4 segments with <c>idRangeOffset != 0</c>
/// (2-byte entries), and the inline per-code arrays of formats 0 (1-byte entries), 6, and 10 (2-byte
/// entries).
/// </summary>
public sealed class SfntCmapGlyphArrayRange : ISfntCmapRange
{
    private readonly byte[] _array;
    private readonly int _entryByteWidth;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapGlyphArrayRange"/> class.
    /// </summary>
    /// <param name="startCode">The first character code in this range, inclusive.</param>
    /// <param name="endCode">The last character code in this range, inclusive.</param>
    /// <param name="idDelta">The value added to the glyph id read from the array, before the modulo-65536 wrap.</param>
    /// <param name="array">The glyph id array, sized to exactly this range's entries starting at <paramref name="startCode"/>.</param>
    /// <param name="entryByteWidth">The width in bytes of one entry: 1 for format 0's byte array, 2 for every other format's big-endian <see cref="ushort"/> array.</param>
    public SfntCmapGlyphArrayRange(int startCode, int endCode, short idDelta, byte[] array, int entryByteWidth)
    {
        StartCode = startCode;
        EndCode = endCode;
        IdDelta = idDelta;
        _array = array;
        _entryByteWidth = entryByteWidth;
    }

    /// <inheritdoc/>
    public int StartCode { get; }

    /// <inheritdoc/>
    public int EndCode { get; }

    /// <summary>
    /// Gets the value added to the glyph id read from the array, before the modulo-65536 wrap.
    /// </summary>
    public short IdDelta { get; }

    /// <inheritdoc/>
    public ushort? GetGid(int code)
    {
        int entryOffset = (code - StartCode) * _entryByteWidth;
        if (entryOffset < 0 || entryOffset + _entryByteWidth > _array.Length)
        {
            return null;
        }

        ushort rawGid = (_entryByteWidth == 1)
            ? _array[entryOffset]
            : (ushort)((_array[entryOffset] << 8) | _array[entryOffset + 1]);

        return (ushort)((rawGid + IdDelta) & 0xFFFF);
    }
}
