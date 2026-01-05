namespace PdfReader.Imaging.Jpx.Model;

/// <summary>
/// Represents a code-block with entropy-coded data from a packet.
/// </summary>
internal sealed class JpxCodeBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    /// <summary>
    /// Entropy-coded data for this code-block from current packet.
    /// </summary>
    public byte[] Data { get; set; }
    
    /// <summary>
    /// Number of zero bit-planes to skip before decoding.
    /// </summary>
    public int ZeroBitPlanes { get; set; }
    
    /// <summary>
    /// Number of coding passes included in this packet.
    /// </summary>
    public int CodingPasses { get; set; }
    
    /// <summary>
    /// Length of code-block data in bytes (from packet header).
    /// </summary>
    public int DataLength { get; set; }
}