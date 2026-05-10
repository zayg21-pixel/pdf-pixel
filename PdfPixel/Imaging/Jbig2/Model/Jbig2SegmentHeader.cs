using System;

namespace PdfPixel.Imaging.Jbig2.Model;

/// <summary>
/// Represents a parsed JBIG2 segment header (ITU-T T.88 Section 7.2.1).
/// </summary>
internal sealed class Jbig2SegmentHeader
{
    /// <summary>
    /// Segment number (unique identifier within the stream).
    /// </summary>
    public uint SegmentNumber { get; set; }

    /// <summary>
    /// Segment type code (see <see cref="Jbig2SegmentType"/>).
    /// </summary>
    public Jbig2SegmentType Type { get; set; }

    /// <summary>
    /// Whether the page association size is long (4 bytes) or short (1 byte).
    /// </summary>
    public bool PageAssociationSizeLong { get; set; }

    /// <summary>
    /// Whether this segment's data should be retained after use.
    /// </summary>
    public bool RetainFlag { get; set; }

    /// <summary>
    /// Page number this segment belongs to. 0 means global.
    /// </summary>
    public int PageAssociation { get; set; }

    /// <summary>
    /// Referred-to segment numbers that this segment depends on.
    /// </summary>
    public uint[] ReferredToSegments { get; set; } = Array.Empty<uint>();

    /// <summary>
    /// Data length for this segment. May be -1 for unknown length (immediate generic region).
    /// </summary>
    public long DataLength { get; set; }

    /// <summary>
    /// Offset in the data stream where the segment data begins.
    /// </summary>
    public int DataOffset { get; set; }

    /// <summary>
    /// Actual row count extracted from the end-of-data marker when segment data length is unknown.
    /// This may differ from the declared region height (e.g., in streaming/striped scenarios).
    /// </summary>
    public uint? ActualRowCount { get; set; }
}
