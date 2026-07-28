namespace PdfPixel.Jpx.Model;

/// <summary>
/// Identifies a single packet's position in the progression order.
/// Packets are the unit the codestream is parsed in; the coefficients they carry are
/// accumulated into the code-blocks they reference rather than stored on the packet.
/// </summary>
internal sealed class JpxPacket
{
    public int Layer { get; set; }
    public int Resolution { get; set; }
    public int Component { get; set; }
    public int PrecinctX { get; set; }
    public int PrecinctY { get; set; }
}
