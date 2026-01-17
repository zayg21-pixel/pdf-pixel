namespace PdfRender.Imaging.Jpg.Model;

/// <summary>
/// Component selector inside an SOS header: component id and DC/AC table selectors.
/// </summary>
internal sealed class JpgScanComponentSpec
{
    /// <summary>
    /// Gets or sets the JPEG component identifier for this scan component.
    /// </summary>
    public byte ComponentId { get; set; }

    /// <summary>
    /// Gets or sets the DC Huffman table selector for this scan component.
    /// </summary>
    public int DcTableId { get; set; }

    /// <summary>
    /// Gets or sets the AC Huffman table selector for this scan component.
    /// </summary>
    public int AcTableId { get; set; }
}
