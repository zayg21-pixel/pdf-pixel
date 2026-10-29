using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Annotations;

/// <summary>
/// Base class for text markup annotations (Highlight, Underline, Squiggly, StrikeOut).
/// </summary>
public abstract class PdfTextMarkupAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextMarkupAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this text markup annotation.</param>
    /// <param name="subtype">The specific text markup annotation subtype.</param>
    private protected PdfTextMarkupAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
        : base(annotationObject, subtype)
    {
        Quadrilaterals = PdfAnnotationGeometryParser.ParseQuadrilaterals(annotationObject.Dictionary.GetArray(PdfTokens.QuadPointsKey));
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the third point of the first highlight quadrilateral.
    /// </summary>
    public override PdfPoint ContentStart => (Quadrilaterals.Length > 0 && Quadrilaterals[0].Length == 4) ? Quadrilaterals[0][2] : base.ContentStart;

    /// <summary>
    /// Gets the collection of quadrilaterals, each represented as an array of four points in two-dimensional space.
    /// </summary>
    public PdfPoint[][] Quadrilaterals { get; }

    /// <summary>
    /// Gets the bounds of <see cref="Quadrilaterals"/> grown by half the width of the lines marked on them, or
    /// <see cref="PdfAnnotation.Rectangle"/> when there are none.
    /// </summary>
    public override PdfRectangle ContentBounds
    {
        get
        {
            if (Quadrilaterals.Length > 0)
            {
                return CreateQuadrilateralsPath().GetBounds().Inflate(new PdfStrokeStyle().LineWidth / 2f);
            }
            else
            {
                return base.ContentBounds;
            }
        }
    }

    /// <summary>
    /// Gets <see cref="Quadrilaterals"/> grown by <paramref name="tolerance"/>, or the hover area of the annotation
    /// when it has none.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance)
    {
        if (Quadrilaterals.Length > 0)
        {
            return CreateQuadrilateralsPath().Inflate(tolerance);
        }
        else
        {
            return base.GetHoverPath(tolerance);
        }
    }

    /// <summary>
    /// Creates one path with a closed contour for each of <see cref="Quadrilaterals"/>.
    /// </summary>
    private PdfPath CreateQuadrilateralsPath()
    {
        PdfPathBuilder quadrilateralsPath = new();
        quadrilateralsPath.AddQuadrilaterals(Quadrilaterals);

        return quadrilateralsPath.ToPath();
    }

    /// <summary>
    /// The frame of the bottom edge of <paramref name="quad"/>: the matrix from its coordinates to the page, with the
    /// x axis along the edge from its midpoint and the y axis to the edge's left, half the edge length, and the
    /// height of the quadrilateral, positive when its top lies on the positive y side.
    /// </summary>
    private protected static (PdfMatrix Matrix, float HalfLength, float Height) GetBaselineFrame(PdfPoint[] quad)
    {
        PdfMatrix lineMatrix = PdfAnnotationGeometry.CreateLineMatrix(quad[1], quad[0]);
        float halfLength = PdfAnnotationGeometry.GetDistance(quad[1], quad[0]) / 2f;
        float height = lineMatrix.Invert().MapPoint(quad[2]).Y;

        return (lineMatrix, halfLength, height);
    }
}
