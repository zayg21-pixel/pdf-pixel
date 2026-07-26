namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A "cmap" range where every code maps to the same glyph id, regardless of code - covers format 13 groups.
/// </summary>
public sealed class SfntCmapConstantRange : ISfntCmapRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapConstantRange"/> class.
    /// </summary>
    /// <param name="startCode">The first character code in this range, inclusive.</param>
    /// <param name="endCode">The last character code in this range, inclusive.</param>
    /// <param name="gid">The glyph id every code in this range maps to.</param>
    public SfntCmapConstantRange(int startCode, int endCode, ushort gid)
    {
        StartCode = startCode;
        EndCode = endCode;
        Gid = gid;
    }

    /// <inheritdoc/>
    public int StartCode { get; }

    /// <inheritdoc/>
    public int EndCode { get; }

    /// <summary>
    /// Gets the glyph id every code in this range maps to.
    /// </summary>
    public ushort Gid { get; }

    /// <inheritdoc/>
    public ushort? GetGid(int code) => Gid;
}
