namespace PdfPixel.Jpeg;

/// <summary>
/// Represents a single JPEG quantization table (8x8) parsed from a DQT segment.
/// Supports 8-bit (Pq=0) and 16-bit (Pq=1) precision entries.
/// </summary>
public sealed class JpegQuantizationTable
{
    /// <summary>
    /// Gets or sets the quantization table identifier (0..3).
    /// </summary>
    public int TableId { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the table uses 16-bit precision entries (Pq=1).
    /// </summary>
    public bool Is16Bit { get; internal set; }

    /// <summary>
    /// Gets the quantization table entries (64 values, row-major order).
    /// </summary>
    public ushort[] Entries { get; } = new ushort[64];
}
