using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Model;

/// <summary>
/// Represents a PDF polygon annotation.
/// </summary>
/// <remarks>
/// Polygon annotations display a closed polygon on the page with multiple vertices.
/// The polygon can be filled with an interior color and stroked with a border.
/// </remarks>
public class PdfPolygonAnnotation : PdfShapeAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolygonAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this polygon annotation.</param>
    public PdfPolygonAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Polygon)
    {
        float[]? vertices = annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey)?.GetFloatArray();

        if (vertices != null)
        {
            Vertices = new PdfPoint[vertices.Length / 2];

            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vertices[i / 2] = new PdfPoint(vertices[i], vertices[i + 1]);
            }
        }
        else
        {
            Vertices = System.Array.Empty<PdfPoint>();
        }

        // TODO: [MEDIUM] Not parsed, so the polygon is drawn as a plain shape without its measurement:
        // - intent: area dimension (IT: PolygonDimension)
        // - measurement dictionary for dimension polygons, units and scale (Measure)
        // - curved outline that replaces Vertices, PDF 2.0 (Path)
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first vertex of the polygon.
    /// </summary>
    protected override PdfPoint ContentStart => (Vertices?.Length > 0) ? Vertices[0] : base.ContentStart;

    /// <inheritdoc/>
    /// <remarks>
    /// When there is no appearance stream, this is the bounding box of <see cref="Vertices"/>.
    /// Otherwise this is the declared /Rect.
    /// </remarks>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (Appearance != null)
            {
                return base.Rectangle;
            }

            PdfRectangle? vertexBounds = PdfRectangle.FromPoints(Vertices);

            if (vertexBounds == null)
            {
                return base.Rectangle;
            }

            float lineWidth = BorderStyle?.StrokeStyle.LineWidth ?? new PdfStrokeStyle().LineWidth;
            PdfRectangle strokeBounds = vertexBounds.Value.Inflate(2f * lineWidth);

            return (PdfRectangle.IntersectsWith(base.Rectangle, strokeBounds)) ? base.Rectangle : strokeBounds;
        }
    }

    /// <summary>
    /// Gets the vertices array containing coordinates of the polygon vertices.
    /// </summary>
    public PdfPoint[] Vertices { get; }

    /// <inheritdoc />
    internal override PdfPath? CreateShapePath(float lineWidth)
    {
        if (Vertices.Length < 3)
        {
            return null;
        }

        PdfPathBuilder path = new();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        path.Close();

        return path.ToPath();
    }

    /// <summary>
    /// Returns a string representation of this polygon annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Polygon Annotation: {Contents}";
        }

        return "Polygon Annotation";
    }
}
