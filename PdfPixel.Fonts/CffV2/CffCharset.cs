using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a parsed CFF charset: the per-GID SID (name-keyed fonts) or CID (CID-keyed fonts)
/// assignment. Retains both the raw table format as read from the font -- so an unmodified charset
/// can be repacked without re-deriving it -- and the fully expanded per-GID mapping used for lookups.
/// </summary>
public class CffCharset
{
    /// <summary>
    /// Gets or sets the charset table format as read from the font (0, 1, or 2), or null if this font
    /// referenced one of the three predefined charsets (ISOAdobe, Expert, ExpertSubset) instead of an
    /// explicit table.
    /// </summary>
    public byte? Format { get; set; }

    /// <summary>
    /// Gets or sets the predefined charset ID (0 = ISOAdobe, 1 = Expert, 2 = ExpertSubset) when this
    /// font referenced a predefined charset instead of an explicit table. Null otherwise.
    /// </summary>
    public int? PredefinedId { get; set; }

    /// <summary>
    /// Gets or sets the fully expanded per-GID mapping: the SID (name-keyed fonts) or CID (CID-keyed
    /// fonts) assigned to each glyph, indexed by GID. GID 0 (.notdef) is always SID/CID 0.
    /// </summary>
    public ushort[] SidsByGid { get; set; } = Array.Empty<ushort>();
}
