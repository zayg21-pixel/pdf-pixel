using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a parsed CFF FDSelect: the GID-to-Font-DICT-index mapping for CID-keyed fonts. Retains
/// the raw table format as read from the font -- so an unmodified FDSelect can be repacked without
/// re-deriving it -- alongside the fully expanded per-GID mapping used for lookups.
/// </summary>
public class CffFdSelect
{
    /// <summary>
    /// Gets or sets the FDSelect table format as read from the font (0 or 3).
    /// </summary>
    public byte Format { get; set; }

    /// <summary>
    /// Gets or sets the fully expanded per-GID mapping: the Font DICT index assigned to each glyph,
    /// indexed by GID.
    /// </summary>
    public int[] FdIndexByGid { get; set; } = Array.Empty<int>();
}
