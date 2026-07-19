using PdfPixel.Geometry;
using PdfPixel.Shading.Model;
using SkiaSharp;

namespace PdfPixel.Commands.Converters;

/// <summary>
/// Converts <see cref="PdfVertices"/> to <see cref="SKVertices"/> for canvas rendering.
/// </summary>
internal static class PdfShadingConverter
{
    /// <summary>
    /// Builds an <see cref="SKVertices"/> equivalent to <paramref name="vertices"/>.
    /// </summary>
    public static SKVertices ToSkVertices(PdfVertices vertices)
    {
        var positions = new SKPoint[vertices.Positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            positions[index] = vertices.Positions[index].ToSkPoint();
        }

        var colors = new SKColor[vertices.Colors.Length];
        for (int index = 0; index < colors.Length; index++)
        {
            colors[index] = vertices.Colors[index].ToSkiaColor();
        }

        return (vertices.Indices != null)
            ? SKVertices.CreateCopy(SKVertexMode.Triangles, positions, null, colors, vertices.Indices)
            : SKVertices.CreateCopy(SKVertexMode.Triangles, positions, colors);
    }
}
