namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// One straight edge of a flattened outline in pixel coordinates, in the direction it was drawn.
/// </summary>
internal readonly struct SnapEdge
{
    public SnapEdge(float startX, float startY, float endX, float endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }

    public float StartX { get; }

    public float StartY { get; }

    public float EndX { get; }

    public float EndY { get; }
}
