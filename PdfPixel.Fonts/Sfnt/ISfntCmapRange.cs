namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A contiguous range of character codes within a "cmap" subtable, resolving a code inside it to a
/// glyph id. Mirrors the range-based encoding "cmap" subtables already use (segments in format 4,
/// groups in formats 12/13) instead of expanding every code into its own dictionary entry.
/// </summary>
public interface ISfntCmapRange
{
    /// <summary>
    /// Gets the first character code in this range, inclusive.
    /// </summary>
    int StartCode { get; }

    /// <summary>
    /// Gets the last character code in this range, inclusive.
    /// </summary>
    int EndCode { get; }

    /// <summary>
    /// Resolves <paramref name="code"/> (which must fall within <see cref="StartCode"/>..<see cref="EndCode"/>)
    /// to a glyph id, or null if this range has no mapping for it.
    /// </summary>
    ushort? GetGid(int code);
}
