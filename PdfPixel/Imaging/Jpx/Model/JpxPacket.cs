namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Represents parsed packet data for a specific layer/resolution/component/precinct.
/// </summary>
internal sealed class JpxPacket
{
    public int Layer { get; set; }
    public int Resolution { get; set; }
    public int Component { get; set; }
    public int PrecinctX { get; set; }
    public int PrecinctY { get; set; }
    
    /// <summary>
    /// Code-blocks included in this packet with their entropy-coded data.
    /// </summary>
    public JpxCodeBlock[] CodeBlocks { get; set; }
}