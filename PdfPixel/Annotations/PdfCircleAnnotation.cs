using PdfPixel.Annotations.Parsing;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF circle annotation.
/// </summary>
public class PdfCircleAnnotation : PdfShapeAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCircleAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this circle annotation.</param>
    internal PdfCircleAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Circle)
    {
        RectangleDifferences = PdfAnnotationGeometryParser.ParseInsets(annotationObject.Dictionary.GetArray(PdfTokens.RectDifferencesKey));
        ContentRectangle = ApplyRectDifferences(Rectangle, RectangleDifferences);
    }

    /// <summary>
    /// Gets the rectangle differences that inset the drawn ellipse from the annotation rectangle.
    /// </summary>
    public PdfRectangle? RectangleDifferences { get; }

    /// <summary>
    /// Gets the effective drawing rectangle after applying <see cref="RectangleDifferences"/>.
    /// </summary>
    public PdfRectangle ContentRectangle { get; }

    /// <inheritdoc/>
    public override PdfPoint ContentStart => new(ContentRectangle.Left, ContentRectangle.Bottom);

    /// <inheritdoc />
    internal override PdfPath? CreateShapePath(float lineWidth)
    {
        PdfPathBuilder shapePath = new();
        shapePath.AddOval(ContentRectangle.Inflate(-lineWidth / 2f));

        return shapePath.ToPath();
    }

    /// <summary>
    /// Returns a string representation of this circle annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Circle Annotation: {Contents}";
        }

        return "Circle Annotation";
    }
}
