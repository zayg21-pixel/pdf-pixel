using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a fully parsed CFF typeface (FontSet): one or more <see cref="CffFont"/> entries
/// sharing a single String INDEX and Global Subr INDEX.
/// </summary>
public class CffTypeface
{
    /// <summary>
    /// Gets or sets the major version number from the CFF header.
    /// </summary>
    public byte MajorVersion { get; set; }

    /// <summary>
    /// Gets or sets the minor version number from the CFF header.
    /// </summary>
    public byte MinorVersion { get; set; }

    /// <summary>
    /// Gets or sets the fonts in this typeface (one per Name/Top DICT INDEX entry).
    /// </summary>
    public CffFont[] Fonts { get; set; } = Array.Empty<CffFont>();

    /// <summary>
    /// Gets or sets the entries of the String INDEX (custom strings referenced by SID from DICTs).
    /// </summary>
    public ReadOnlyMemory<byte>[] Strings { get; set; } = Array.Empty<ReadOnlyMemory<byte>>();

    /// <summary>
    /// Gets or sets the entries of the Global Subr INDEX (charstring subroutines callable via <c>callgsubr</c>).
    /// </summary>
    public ReadOnlyMemory<byte>[] GlobalSubrs { get; set; } = Array.Empty<ReadOnlyMemory<byte>>();
}
