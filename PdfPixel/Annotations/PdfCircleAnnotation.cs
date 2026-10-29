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
    private const float HalfSquareRootOfTwo = 0.70710678f;

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

    /// <summary>
    /// Gets the upper-left point of the ellipse drawn in <see cref="ContentRectangle"/>, halfway between its top and its
    /// left.
    /// </summary>
    public override PdfPoint ContentStart
    {
        get
        {
            float offsetX = ContentRectangle.Width / 2f * HalfSquareRootOfTwo;
            float offsetY = ContentRectangle.Height / 2f * HalfSquareRootOfTwo;

            return new PdfPoint(ContentRectangle.MidX - offsetX, ContentRectangle.MidY + offsetY);
        }
    }

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
