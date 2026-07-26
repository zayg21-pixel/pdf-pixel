namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A "cmap" range whose glyph id is <c>(code + IdDelta) &amp; 0xFFFF</c>, wrapping modulo 65536 - covers
/// format 4 segments with <c>idRangeOffset == 0</c>, whose on-disk <c>idDelta</c> is itself a 16-bit
/// signed value and intentionally wraps.
/// </summary>
public sealed class SfntCmapDeltaRange : ISfntCmapRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapDeltaRange"/> class.
    /// </summary>
    /// <param name="startCode">The first character code in this range, inclusive.</param>
    /// <param name="endCode">The last character code in this range, inclusive.</param>
    /// <param name="idDelta">The value added to a code to get its glyph id, before the modulo-65536 wrap.</param>
    public SfntCmapDeltaRange(int startCode, int endCode, short idDelta)
    {
        StartCode = startCode;
        EndCode = endCode;
        IdDelta = idDelta;
    }

    /// <inheritdoc/>
    public int StartCode { get; }

    /// <inheritdoc/>
    public int EndCode { get; }

    /// <summary>
    /// Gets the value added to a code to get its glyph id, before the modulo-65536 wrap.
    /// </summary>
    public short IdDelta { get; }

    /// <inheritdoc/>
    public ushort? GetGid(int code) => (ushort)((code + IdDelta) & 0xFFFF);
}
