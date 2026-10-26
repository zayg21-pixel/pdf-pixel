namespace PdfPixel.Jpeg;

/// <summary>
/// Represents a single JPEG Huffman table as declared in a DHT segment.
/// Contains canonical code lengths and the associated values in increasing code order.
/// </summary>
public sealed class JpegHuffmanTable
{
    /// <summary>
    /// Maximum JPEG Huffman code length in bits (per JPEG T.81 §C.1).
    /// </summary>
    public const int MaxCodeLength = 16;

    /// <summary>
    /// Table class: 0 = DC, 1 = AC.
    /// </summary>
    public int TableClass { get; set; }

    /// <summary>
    /// Table identifier (0..3).
    /// </summary>
    public int TableId { get; set; }

    /// <summary>
    /// Code length counts L1..L16 (number of codes for each bit-length 1..16).
    /// </summary>
    public byte[] CodeLengthCounts { get; } = new byte[MaxCodeLength];

    /// <summary>
    /// Huffman values (symbols) in the canonical increasing code order.
    /// </summary>
    public byte[] Values { get; set; } = [];
}
