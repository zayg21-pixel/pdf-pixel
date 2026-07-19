using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
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

        PdfAnnotationBorderParser.ApplyBorderEffect(BorderStyle, annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
        // TODO: [MEDIUM] IT (Intent: PolygonCloud, PolygonDimension), Measure
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the first vertex of the polygon.
    /// </summary>
    protected override PdfPoint ContentStart => (Vertices?.Length > 0) ? Vertices[0] : base.ContentStart;

    /// <summary>
    /// Gets the vertices array containing coordinates of the polygon vertices.
    /// </summary>
    public PdfPoint[] Vertices { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (Vertices.Length < 3)
        {
            return false;
        }

        PdfPath path = new();

        path.MoveTo(Vertices[0]);

        for (int i = 1; i < Vertices.Length; i++)
        {
            path.LineTo(Vertices[i]);
        }

        path.Close();

        PdfColor interiorColor = ResolveInteriorColor(page);

        if (interiorColor != PdfColors.Transparent)
        {
            processor.Process(new DrawPathCommand(path, PdfAnnotationPaintFactory.CreateFillPaint(interiorColor)));
        }

        if (BorderStyle != null && BorderStyle.LineWidth > 0 && Color?.Length > 0)
        {
            PdfColor strokeColor = ResolveColor(page, PdfColors.Black);

            PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(strokeColor, BorderStyle);

            processor.Process(new DrawPathCommand(path, strokePaint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this polygon annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Polygon Annotation: {contentsText}";
        }

        return "Polygon Annotation";
    }
}
