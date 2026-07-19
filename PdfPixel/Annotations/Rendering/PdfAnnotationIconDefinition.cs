using PdfPixel.Annotations.Models;
using PdfPixel.Geometry;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// A complete icon definition parsed from a resource XML element.
/// </summary>
internal sealed class PdfAnnotationIconDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotationIconDefinition"/> class.
    /// </summary>
    public PdfAnnotationIconDefinition(
        string name,
        float width,
        float height,
        PdfAnnotationVisualStateKind visualState,
        in PdfMatrix viewportMatrix,
        PdfAnnotationIconPath[] paths)
    {
        Name = name;
        Width = width;
        Height = height;
        VisualState = visualState;
        ViewportMatrix = viewportMatrix;
        Paths = paths;
    }

    /// <summary>
    /// Gets the name identifying this icon.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the natural width of the icon in user units.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Gets the natural height of the icon in user units.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Gets the visual state this icon represents.
    /// </summary>
    public PdfAnnotationVisualStateKind VisualState { get; }

    /// <summary>
    /// Gets the matrix that maps path coordinates to the icon's normalized coordinate space.
    /// Derived from the <c>viewBox</c> attribute; <see cref="PdfMatrix.Identity"/> when absent.
    /// </summary>
    public PdfMatrix ViewportMatrix { get; }

    /// <summary>
    /// Gets the ordered list of paths that make up the icon.
    /// </summary>
    public PdfAnnotationIconPath[] Paths { get; }
}
