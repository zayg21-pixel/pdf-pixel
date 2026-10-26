namespace PdfPixel.Jpeg;

/// <summary>
/// Component selector inside an SOS header: component id and DC/AC table selectors.
/// </summary>
public sealed class JpegScanComponentSpec
{
    /// <summary>
    /// Gets or sets the JPEG component identifier for this scan component.
    /// </summary>
    public byte ComponentId { get; internal set; }

    /// <summary>
    /// Gets or sets the DC Huffman table selector for this scan component.
    /// </summary>
    public int DcTableId { get; internal set; }

    /// <summary>
    /// Gets or sets the AC Huffman table selector for this scan component.
    /// </summary>
    public int AcTableId { get; internal set; }
}
