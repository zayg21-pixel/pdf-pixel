using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Cff;

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

    /// <summary>
    /// Resolves a SID to its glyph name: one of the 391 predefined CFF standard strings if the SID
    /// falls within that range, otherwise an entry from the font's own String INDEX.
    /// </summary>
    /// <param name="sid">The string ID to resolve.</param>
    /// <param name="customStrings">The font's String INDEX entries (<see cref="CffTypeface.Strings"/>).</param>
    public static PdfFontString ResolveGlyphName(ushort sid, ReadOnlyMemory<byte>[] customStrings)
    {
        if (customStrings == null)
        {
            throw new ArgumentNullException(nameof(customStrings));
        }

        if (sid < CffStandardStrings.StandardStrings.Length)
        {
            return CffStandardStrings.StandardStrings[sid];
        }

        int customIndex = sid - CffStandardStrings.StandardStrings.Length;
        return ((uint)customIndex < (uint)customStrings.Length) ? customStrings[customIndex] : default;
    }
}
