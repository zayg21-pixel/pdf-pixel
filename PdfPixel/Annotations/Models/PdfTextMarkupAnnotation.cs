using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Base class for text markup annotations (Highlight, Underline, Squiggly, StrikeOut).
/// </summary>
/// <remarks>
/// Text markup annotations are used to mark up text in a PDF document. They use QuadPoints
/// to define the regions of text to be marked. QuadPoints specify quadrilaterals that
/// encompass the marked text.
/// </remarks>
public abstract class PdfTextMarkupAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextMarkupAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this text markup annotation.</param>
    /// <param name="subtype">The specific text markup annotation subtype.</param>
    protected PdfTextMarkupAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
        : base(annotationObject, subtype)
    {
        var quadPoints = annotationObject.Dictionary.GetArray(PdfTokens.QuadPointsKey)?.GetFloatArray();
        Quadrilaterals = GetQuadrilaterals(quadPoints);
    }

    protected override SKPoint ContentStart => Quadrilaterals.Length > 0 && Quadrilaterals[0].Length == 4 ? Quadrilaterals[0][2] : base.ContentStart;

    /// <summary>
    /// Gets the collection of quadrilaterals, each represented as an array of four points in two-dimensional space.
    /// </summary>
    /// <remarks>Each quadrilateral is defined by an array of four <see cref="SKPoint"/> instances, specifying
    /// its vertices in order. This property can be used to access the geometric outlines of annotated regions for
    /// rendering or further processing. The order of points in each array determines the shape and orientation of the
    /// quadrilateral.</remarks>
    public SKPoint[][] Quadrilaterals { get; }

    private static SKPoint[][] GetQuadrilaterals(float[] quadPoints)
    {
        if (quadPoints == null || quadPoints.Length < 8 || quadPoints.Length % 8 != 0)
        {
            return [];
        }

        var quadCount = quadPoints.Length / 8;
        var quads = new SKPoint[quadCount][];

        for (int i = 0; i < quadCount; i++)
        {
            var offset = i * 8;
            quads[i] =
            [
                new SKPoint(quadPoints[offset + 6], quadPoints[offset + 7]),
                new SKPoint(quadPoints[offset + 4], quadPoints[offset + 5]),
                new SKPoint(quadPoints[offset + 0], quadPoints[offset + 1]),
                new SKPoint(quadPoints[offset + 2], quadPoints[offset + 3]),
            ];
        }

        return quads;
    }
}
