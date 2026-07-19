using PdfPixel.Color;
using PdfPixel.Geometry;

namespace PdfPixel.Shading.Model;

/// <summary>
/// A batch of tessellated triangles with per-vertex colors, produced by mesh shading decoding.
/// </summary>
internal sealed class PdfVertices
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfVertices"/> class.
    /// </summary>
    /// <param name="positions">Vertex positions in shading space.</param>
    /// <param name="colors">Per-vertex colors, one per entry in <paramref name="positions"/>.</param>
    /// <param name="indices">Triangle indices into <paramref name="positions"/>/<paramref name="colors"/>; null when vertices are already in triangle order.</param>
    public PdfVertices(PdfPoint[] positions, PdfColor[] colors, ushort[]? indices)
    {
        Positions = positions;
        Colors = colors;
        Indices = indices;
    }

    /// <summary>
    /// Gets the vertex positions in shading space.
    /// </summary>
    public PdfPoint[] Positions { get; }

    /// <summary>
    /// Gets the per-vertex colors, one per entry in <see cref="Positions"/>.
    /// </summary>
    public PdfColor[] Colors { get; }

    /// <summary>
    /// Gets the triangle indices into <see cref="Positions"/>/<see cref="Colors"/>; null when vertices are already in triangle order.
    /// </summary>
    public ushort[]? Indices { get; }
}
