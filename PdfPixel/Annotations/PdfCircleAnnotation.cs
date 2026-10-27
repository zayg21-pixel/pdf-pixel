using PdfPixel.Annotations.Parsing;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF circle annotation.
/// </summary>
/// <remarks>
/// Circle annotations display an ellipse on the page. When the annotation has no appearance stream,
/// the ellipse is drawn to fit within the annotation rectangle using the specified color and border style.
/// </remarks>
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

    /// <inheritdoc />
    internal override PdfPath? CreateShapePath(float lineWidth)
    {
        float inset = (lineWidth / 2f) + ((RectangleDifferences == null) ? BorderEffect.GetOutset(lineWidth) : 0f);

        PdfPathBuilder shapePath = new();
        shapePath.AddOval(ContentRectangle.Inflate(-inset));

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
