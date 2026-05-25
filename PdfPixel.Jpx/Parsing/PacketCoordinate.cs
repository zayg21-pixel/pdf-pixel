namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Represents coordinates for a single packet in the progression order.
/// </summary>
internal readonly struct PacketCoordinate
{
    public readonly int Layer;
    public readonly int Resolution;
    public readonly int Component;
    public readonly int PrecinctX;
    public readonly int PrecinctY;

    public PacketCoordinate(int layer, int resolution, int component, int precinctX, int precinctY)
    {
        Layer = layer;
        Resolution = resolution;
        Component = component;
        PrecinctX = precinctX;
        PrecinctY = precinctY;
    }
}
