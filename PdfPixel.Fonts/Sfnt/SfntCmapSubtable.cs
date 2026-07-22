using PdfPixel.Fonts.Model;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a single subtable within a font's "cmap" table.
/// </summary>
public class SfntCmapSubtable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapSubtable"/> class.
    /// </summary>
    /// <param name="format">The subtable format number (e.g. 0, 4, 6) as defined by the OpenType spec.</param>
    /// <param name="platformId">The subtable record's platform ID.</param>
    /// <param name="encodingId">The subtable record's platform-specific encoding ID.</param>
    /// <param name="encoding">The PDF font encoding implied by <paramref name="platformId"/>/<paramref name="encodingId"/>, or null if unrecognized.</param>
    /// <param name="codeToGid">The parsed code-to-GID mapping, or null if the format is unsupported.</param>
    public SfntCmapSubtable(ushort format, ushort platformId, ushort encodingId, PdfFontEncoding? encoding, SortedDictionary<int, ushort>? codeToGid)
    {
        Format = format;
        PlatformId = platformId;
        EncodingId = encodingId;
        Encoding = encoding;
        CodeToGid = codeToGid;
    }

    /// <summary>
    /// Gets the subtable format number (e.g. 0, 4, 6) as defined by the OpenType spec.
    /// </summary>
    public ushort Format { get; }

    /// <summary>
    /// Gets the subtable record's platform ID.
    /// </summary>
    public ushort PlatformId { get; }

    /// <summary>
    /// Gets the subtable record's platform-specific encoding ID.
    /// </summary>
    public ushort EncodingId { get; }

    /// <summary>
    /// Gets the PDF font encoding implied by <see cref="PlatformId"/>/<see cref="EncodingId"/>, or null
    /// if unrecognized.
    /// </summary>
    public PdfFontEncoding? Encoding { get; }

    /// <summary>
    /// Gets the parsed code-to-GID mapping, or null if the format is unsupported.
    /// </summary>
    public SortedDictionary<int, ushort>? CodeToGid { get; }
}
