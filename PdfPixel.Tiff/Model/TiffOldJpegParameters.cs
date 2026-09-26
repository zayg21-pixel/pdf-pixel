namespace PdfPixel.Tiff.Model;

/// <summary>
/// JPEG tags of the original TIFF 6.0 JPEG scheme (compression 6), with the tables they point to read from the file.
/// </summary>
public sealed class TiffOldJpegParameters
{
    /// <summary>
    /// Gets or sets the JPEG process (JPEGProc): 1 for baseline sequential, 14 for lossless.
    /// </summary>
    public int Process { get; set; } = 1;

    /// <summary>
    /// Gets or sets the JPEG interchange format stream (JPEGInterchangeFormat), or null when the tag is absent.
    /// </summary>
    public byte[]? InterchangeFormat { get; set; }

    /// <summary>
    /// Gets or sets the restart interval in MCUs (JPEGRestartInterval), or null when the tag is absent.
    /// </summary>
    public int? RestartInterval { get; set; }

    /// <summary>
    /// Gets or sets the 64-byte quantization table of each component, in zigzag order (JPEGQTables).
    /// </summary>
    public byte[][]? QuantizationTables { get; set; }

    /// <summary>
    /// Gets or sets the DC Huffman table of each component: 16 code counts, then the values (JPEGDCTables).
    /// </summary>
    public byte[][]? DcHuffmanTables { get; set; }

    /// <summary>
    /// Gets or sets the AC Huffman table of each component: 16 code counts, then the values (JPEGACTables).
    /// </summary>
    public byte[][]? AcHuffmanTables { get; set; }
}
