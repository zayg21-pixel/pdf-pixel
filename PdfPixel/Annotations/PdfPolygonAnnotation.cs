using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Measures;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF polygon annotation.
/// </summary>
public class PdfPolygonAnnotation : PdfShapeAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPolygonAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this polygon annotation.</param>
    internal PdfPolygonAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Polygon)
    {
        Vertices = PdfPoint.ParsePoints(annotationObject.Dictionary.GetArray(PdfTokens.VerticesKey));
        Path = PdfAnnotationGeometryParser.ParsePath(annotationObject.Dictionary.GetArray(PdfTokens.PathKey));

        if (Path != null)
        {
            DrawingPath = PdfAnnotationGeometry.ClosePath(Path);
        }
        else
        {
            DrawingPath = PdfAnnotationGeometry.CreatePolygonPath(Vertices);
        }

        Measure = PdfMeasure.GetMeasure(annotationObject.Dictionary, PdfTokens.MeasureKey);
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first point of the polygon outline.
    /// </summary>
    public override PdfPoint ContentStart => PdfAnnotationGeometry.GetStartPoint(DrawingPath) ?? base.ContentStart;

    /// <summary>
    /// Gets the declared rectangle, or the stroked bounds of the polygon outline when there is no appearance
    /// stream and the declared rectangle does not overlap them.
    /// </summary>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (Appearance != null)
            {
                return base.Rectangle;
            }

            float lineWidth = BorderStyle?.StrokeStyle.LineWidth ?? new PdfStrokeStyle().LineWidth;

            return PdfAnnotationGeometry.GetFallbackRectangle(base.Rectangle, DrawingPath, lineWidth);
        }
    }

    /// <summary>
    /// Gets the vertices array containing coordinates of the polygon vertices.
    /// </summary>
    public PdfPoint[] Vertices { get; }

    /// <summary>
    /// Gets the polygon outline built from path operators, PDF 2.0, or null when absent or malformed.
    /// </summary>
    public PdfPath? Path { get; }

    /// <summary>
    /// Gets the scale and units that apply to the polygon (/Measure), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMeasure? Measure { get; }

    /// <summary>
    /// Gets the closed outline that is drawn: <see cref="Path"/> when present, otherwise <see cref="Vertices"/>.
    /// Null when neither defines a polygon.
    /// </summary>
    internal PdfPath? DrawingPath { get; }

    /// <inheritdoc />
    internal override PdfPath? CreateShapePath(float lineWidth) => DrawingPath;

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
