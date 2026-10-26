namespace PdfPixel.Jpx.Packets;

/// <summary>
/// Represents coordinates for a single packet in the progression order.
/// </summary>
internal readonly struct JpxPacketCoordinate
{
    public readonly int Layer;
    public readonly int Resolution;
    public readonly int Component;
    public readonly int PrecinctX;
    public readonly int PrecinctY;

    public JpxPacketCoordinate(int layer, int resolution, int component, int precinctX, int precinctY)
    {
        Layer = layer;
        Resolution = resolution;
        Component = component;
        PrecinctX = precinctX;
        PrecinctY = precinctY;
    }
}
