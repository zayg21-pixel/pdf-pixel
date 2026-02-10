namespace PdfPixel.Imaging.Jpg.Model;

/// <summary>
/// ICC segment bookkeeping for APP2 ICC_PROFILE; the actual profile bytes can be reconstructed by caller.
/// </summary>
internal sealed class IccSegmentInfo
{
    /// <summary>
    /// Gets or sets the sequence number of this ICC profile segment (1-based).
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the total number of ICC profile segments in the JPEG file.
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// Gets or sets the raw data bytes for this ICC profile segment.
    /// </summary>
    public byte[] Data { get; set; }
}
