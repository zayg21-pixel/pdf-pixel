using PdfPixel.Color;

namespace PdfPixel.Shading.Model;

/// <summary>
/// The color data of a single mesh vertex or patch corner (shading types 4 to 7).
/// </summary>
/// <remarks>
/// A shading that carries a function stores one parametric value per vertex instead of color
/// components, and the function is evaluated only after that value has been interpolated across the
/// mesh. <see cref="Parameter"/> holds the value to interpolate in that case and is 0 for a shading
/// without a function, whose vertices interpolate <see cref="Color"/> directly.
/// </remarks>
internal readonly struct MeshVertexColor
{
    /// <summary>
    /// Initializes color data for one vertex.
    /// </summary>
    /// <param name="color">The vertex's color.</param>
    /// <param name="parameter">The vertex's parametric value, or 0 when the shading carries no function.</param>
    public MeshVertexColor(in PdfColor color, float parameter)
    {
        Color = color;
        Parameter = parameter;
    }

    /// <summary>
    /// Gets the vertex's color.
    /// </summary>
    public PdfColor Color { get; }

    /// <summary>
    /// Gets the vertex's parametric value, or 0 when the shading carries no function.
    /// </summary>
    public float Parameter { get; }
}
