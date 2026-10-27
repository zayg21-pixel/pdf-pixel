using PdfPixel.Annotations.Parsing;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF square annotation.
/// </summary>
public class PdfSquareAnnotation : PdfShapeAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSquareAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this square annotation.</param>
    internal PdfSquareAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Square)
    {
        RectangleDifferences = PdfAnnotationGeometryParser.ParseInsets(annotationObject.Dictionary.GetArray(PdfTokens.RectDifferencesKey));
        ContentRectangle = ApplyRectDifferences(Rectangle, RectangleDifferences);
    }

    /// <summary>
    /// Gets the rectangle differences that inset the drawn shape from the annotation rectangle.
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
        shapePath.AddRect(ContentRectangle.Inflate(-inset));

        return shapePath.ToPath();
    }

    /// <summary>
    /// Returns a string representation of this square annotation.
    /// </summary>
    /// <returns>A string containing the annotation type.</returns>
    public override string ToString()
    {
        if (Contents != null)
        {
            return $"Square Annotation: {Contents}";
        }

        return "Square Annotation";
    }
}
