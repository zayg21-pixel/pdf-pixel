using PdfPixel.Annotations.Parsing;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System;

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
    protected override PdfPoint ContentStart => (Quadrilaterals.Length > 0 && Quadrilaterals[0].Length == 4) ? Quadrilaterals[0][2] : base.ContentStart;

    /// <summary>
    /// Gets the collection of quadrilaterals, each represented as an array of four points in two-dimensional space.
    /// </summary>
    public PdfPoint[][] Quadrilaterals { get; }

    /// <summary>
    /// Gets the declared rectangle, or the union of the quadrilateral bounds when there is no appearance stream.
    /// </summary>
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
}
