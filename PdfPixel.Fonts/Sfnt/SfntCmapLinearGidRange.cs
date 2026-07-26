namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A "cmap" range whose glyph id is <c>StartGid + (code - StartCode)</c>, with no wraparound - covers
/// format 12 groups, whose codes and glyph ids can both exceed 16 bits, unlike format 4's
/// <see cref="SfntCmapDeltaRange"/>.
/// </summary>
public sealed class SfntCmapLinearGidRange : ISfntCmapRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapLinearGidRange"/> class.
    /// </summary>
    /// <param name="startCode">The first character code in this range, inclusive.</param>
    /// <param name="endCode">The last character code in this range, inclusive.</param>
    /// <param name="startGid">The glyph id <see cref="StartCode"/> maps to; later codes map to consecutive glyph ids.</param>
    public SfntCmapLinearGidRange(int startCode, int endCode, int startGid)
    {
        StartCode = startCode;
        EndCode = endCode;
        StartGid = startGid;
    }

    /// <inheritdoc/>
    public int StartCode { get; }

    /// <inheritdoc/>
    public int EndCode { get; }

    /// <summary>
    /// Gets the glyph id <see cref="StartCode"/> maps to; later codes map to consecutive glyph ids.
    /// </summary>
    public int StartGid { get; }

    /// <inheritdoc/>
    public ushort? GetGid(int code)
    {
        long gid = StartGid + (code - StartCode);
        return (gid >= 0 && gid <= ushort.MaxValue) ? (ushort)gid : null;
    }
}
