using System.Collections.Generic;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// The glyph subset and character mapping a repack writes into the font it produces.
/// </summary>
public sealed class SfntPdfTypefaceRepackParameters
{
    /// <summary>
    /// The source glyph ids to keep, in the order the repacked font numbers them: the glyph written at
    /// id i is the source font's <c>GlyphOrder[i]</c>. Null keeps every glyph at the id it already had.
    /// </summary>
    public IReadOnlyList<ushort>? GlyphOrder { get; set; }

    /// <summary>
    /// The character code to glyph id mapping the written "cmap" states, in the repacked font's own
    /// glyph ids. Null writes an empty placeholder "cmap".
    /// </summary>
    public IReadOnlyDictionary<int, ushort>? CodeToGid { get; set; }

    /// <summary>
    /// The platform the written "cmap" subtable declares: 3 for Windows, 1 for Macintosh.
    /// </summary>
    public ushort CmapPlatformId { get; set; } = 3;

    /// <summary>
    /// The encoding the written "cmap" subtable declares within its platform: for Windows, 1 for
    /// Unicode BMP and 0 for Symbol.
    /// </summary>
    public ushort CmapEncodingId { get; set; } = 1;
}
