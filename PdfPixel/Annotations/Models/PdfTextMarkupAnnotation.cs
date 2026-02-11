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
        QuadPoints = annotationObject.Dictionary.GetArray(PdfTokens.QuadPointsKey)?.GetFloatArray();
    }

    /// <summary>
    /// Gets the QuadPoints array defining the marked text regions.
    /// </summary>
    /// <remarks>
    /// QuadPoints is an array of 8 × n numbers specifying the coordinates of n quadrilaterals
    /// in default user space. Each quadrilateral encompasses a word or group of contiguous words
    /// in the text underlying the annotation. The coordinates for each quadrilateral are given
    /// in the order: x1 y1 x2 y2 x3 y3 x4 y4 specifying the four vertices in counterclockwise order.
    /// The text orientation is from (x1, y1) to (x2, y2).
    /// </remarks>
    public float[] QuadPoints { get; }

    /// <summary>
    /// Parses QuadPoints into individual quadrilaterals.
    /// </summary>
    /// <returns>An array of SKPoint arrays, where each inner array contains 4 points forming a quadrilateral in spec order: bottom-left, bottom-right, top-right, top-left.</returns>
    /// <remarks>
    /// Returns points in the order specified by the PDF spec:
    /// - Point 0 (x1, y1): Bottom-left corner
    /// - Point 1 (x2, y2): Bottom-right corner (text baseline edge is from point 0 to 1)
    /// - Point 2 (x3, y3): Top-right corner
    /// - Point 3 (x4, y4): Top-left corner
    /// Points are in counter-clockwise order with text orientation from (x1,y1) to (x2,y2).
    /// </remarks>
    protected SKPoint[][] GetQuadrilaterals()
    {
        if (QuadPoints == null || QuadPoints.Length < 8 || QuadPoints.Length % 8 != 0)
        {
            return [];
        }

        var quadCount = QuadPoints.Length / 8;
        var quads = new SKPoint[quadCount][];

        for (int i = 0; i < quadCount; i++)
        {
            var offset = i * 8;
            quads[i] =
            [
                new SKPoint(QuadPoints[offset + 6], QuadPoints[offset + 7]),
                new SKPoint(QuadPoints[offset + 4], QuadPoints[offset + 5]),
                new SKPoint(QuadPoints[offset + 0], QuadPoints[offset + 1]),
                new SKPoint(QuadPoints[offset + 2], QuadPoints[offset + 3]),
            ];
        }

        return quads;
    }
}
