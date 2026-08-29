using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;

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
        float[]? quadPoints = annotationObject.Dictionary.GetArray(PdfTokens.QuadPointsKey)?.GetFloatArray();
        Quadrilaterals = GetQuadrilaterals(quadPoints);
    }

    /// <summary>
    /// Gets the starting point for bubble placement, using the third point of the first highlight quadrilateral.
    /// </summary>
    protected override PdfPoint ContentStart => (Quadrilaterals.Length > 0 && Quadrilaterals[0].Length == 4) ? Quadrilaterals[0][2] : base.ContentStart;

    /// <summary>
    /// Gets the collection of quadrilaterals, each represented as an array of four points in two-dimensional space.
    /// </summary>
    /// <remarks>Each quadrilateral is defined by an array of four <see cref="PdfPoint"/> instances, specifying
    /// its vertices in order. This property can be used to access the geometric outlines of annotated regions for
    /// rendering or further processing. The order of points in each array determines the shape and orientation of the
    /// quadrilateral.</remarks>
    public PdfPoint[][] Quadrilaterals { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// When there is no appearance stream, this is the union of <see cref="GetQuadBounds"/> across
    /// <see cref="Quadrilaterals"/>. Otherwise this is the declared /Rect.
    /// </remarks>
    public override PdfRectangle Rectangle
    {
        get
        {
            if (Appearance != null || Quadrilaterals.Length == 0)
            {
                return base.Rectangle;
            }

            PdfRectangle bounds = GetQuadBounds(Quadrilaterals[0]);

            for (int index = 1; index < Quadrilaterals.Length; index++)
            {
                bounds = PdfRectangle.Union(bounds, GetQuadBounds(Quadrilaterals[index]));
            }

            return bounds;
        }
    }

    /// <summary>
    /// Returns the bounds of a single quadrilateral's fallback-rendered geometry, in PDF user space.
    /// </summary>
    /// <remarks>
    /// The default is the quadrilateral's own corner bounding box.
    /// </remarks>
    protected virtual PdfRectangle GetQuadBounds(PdfPoint[] quad)
    {
        if (quad == null)
        {
            throw new ArgumentNullException(nameof(quad));
        }

        float left = Math.Min(Math.Min(quad[0].X, quad[1].X), Math.Min(quad[2].X, quad[3].X));
        float right = Math.Max(Math.Max(quad[0].X, quad[1].X), Math.Max(quad[2].X, quad[3].X));
        float minY = Math.Min(Math.Min(quad[0].Y, quad[1].Y), Math.Min(quad[2].Y, quad[3].Y));
        float maxY = Math.Max(Math.Max(quad[0].Y, quad[1].Y), Math.Max(quad[2].Y, quad[3].Y));

        return new PdfRectangle(left, minY, right, maxY);
    }

    private static PdfPoint[][] GetQuadrilaterals(float[]? quadPoints)
    {
        if (quadPoints == null || quadPoints.Length < 8 || quadPoints.Length % 8 != 0)
        {
            return System.Array.Empty<PdfPoint[]>();
        }

        int quadCount = quadPoints.Length / 8;
        var quads = new PdfPoint[quadCount][];

        for (int i = 0; i < quadCount; i++)
        {
            int offset = i * 8;
            quads[i] =
            [
                new PdfPoint(quadPoints[offset + 6], quadPoints[offset + 7]),
                new PdfPoint(quadPoints[offset + 4], quadPoints[offset + 5]),
                new PdfPoint(quadPoints[offset + 0], quadPoints[offset + 1]),
                new PdfPoint(quadPoints[offset + 2], quadPoints[offset + 3])
            ];
        }

        return quads;
    }
}
