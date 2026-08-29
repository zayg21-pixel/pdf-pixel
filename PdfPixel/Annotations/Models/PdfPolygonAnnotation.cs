using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF polygon annotation.
/// </summary>
/// <remarks>
/// Polygon annotations display a closed polygon on the page with multiple vertices.
/// The polygon can be filled with an interior color and stroked with a border.
/// </remarks>
public class PdfPolygonAnnotation : PdfAnnotationBase
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

        BorderEffect = PdfAnnotationBorderParser.ParseBorderEffect(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        // TODO: [MEDIUM] IT (Intent: PolygonCloud, PolygonDimension), Measure
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

    /// <summary>
    /// Gets the parsed border effect (BE entry).
    /// </summary>
    public PdfAnnotationBorderEffect BorderEffect { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices.Length < 3)
        {
            return false;
        }

        PdfPathBuilder path = new();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        path.Close();

        PdfPath builtPath = path.ToPath();

        PdfColor interiorColor = ResolveInteriorColor(page);

        if (interiorColor != PdfColors.Transparent)
        {
            processor.Process(new DrawPathCommand(builtPath, PdfAnnotationPaintFactory.CreateFillPaint(interiorColor)));
        }

        if (BorderStyle != null && BorderStyle.StrokeStyle.LineWidth > 0 && Color?.Length > 0)
        {
            PdfColor strokeColor = ResolveColor(page, PdfColors.Black);

            PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(strokeColor, BorderStyle.StrokeStyle, BorderEffect);

            processor.Process(new DrawPathCommand(builtPath, strokePaint));
        }

        return true;
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
