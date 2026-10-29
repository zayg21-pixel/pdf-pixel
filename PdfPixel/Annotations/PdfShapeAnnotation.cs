using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using System;

namespace PdfPixel.Annotations;

/// <summary>
/// Base class for annotations drawn as a closed shape with an interior color and a border.
/// </summary>
public abstract class PdfShapeAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShapeAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this annotation.</param>
    /// <param name="subtype">The annotation subtype.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="annotationObject"/> is null.</exception>
    private protected PdfShapeAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
        : base(annotationObject, subtype)
    {
        if (annotationObject == null)
        {
            throw new ArgumentNullException(nameof(annotationObject));
        }

        BorderEffect = PdfAnnotationBorderParser.ParseBorderEffect(annotationObject.Dictionary.GetDictionary(PdfTokens.BorderEffectKey));
    }

    /// <summary>
    /// Gets the parsed border effect (BE entry).
    /// </summary>
    public PdfAnnotationBorderEffect BorderEffect { get; }

    /// <summary>
    /// Gets the stroked bounds of the shape grown by the reach of <see cref="BorderEffect"/>, or
    /// <see cref="PdfAnnotation.Rectangle"/> when the annotation defines no shape.
    /// </summary>
    public override PdfRectangle ContentBounds
    {
        get
        {
            PdfStrokeStyle strokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
            PdfPath? shapePath = CreateShapePath(strokeStyle.LineWidth);

            if (shapePath != null)
            {
                return shapePath.GetBounds().InflateForStroke(strokeStyle).Inflate(BorderEffect.GetOutset(strokeStyle.LineWidth));
            }
            else
            {
                return base.ContentBounds;
            }
        }
    }

    /// <summary>
    /// Gets the shape grown by half its border width plus <paramref name="tolerance"/>, or the hover area of the
    /// annotation when it defines no shape.
    /// </summary>
    /// <param name="tolerance">Distance the hover area reaches beyond what is drawn.</param>
    internal override PdfPath GetHoverPath(float tolerance)
    {
        PdfStrokeStyle strokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
        PdfPath? shapePath = CreateShapePath(strokeStyle.LineWidth);

        if (shapePath != null)
        {
            float borderReach = (HasBorder(strokeStyle)) ? strokeStyle.LineWidth / 2f : 0f;
            return shapePath.Inflate(borderReach + tolerance);
        }
        else
        {
            return base.GetHoverPath(tolerance);
        }
    }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfStrokeStyle strokeStyle = BorderStyle?.StrokeStyle ?? new PdfStrokeStyle();
        PdfPath? shapePath = CreateShapePath(strokeStyle.LineWidth);

        if (shapePath == null)
        {
            return false;
        }

        PdfPath effectPath = BorderEffect.Apply(shapePath, strokeStyle.LineWidth);
        PdfColor interiorColor = ResolveInteriorColor(page);

        if (interiorColor != PdfColors.Transparent)
        {
            processor.Process(new DrawPathCommand(effectPath, PdfAnnotationPaintFactory.CreateFillPaint(interiorColor)));
        }

        if (HasBorder(strokeStyle))
        {
            PdfColor strokeColor = ResolveColor(page);
            PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(strokeColor, strokeStyle, BorderEffect);
            processor.Process(new DrawPathCommand(effectPath, strokePaint));
        }

        return true;
    }

    /// <summary>
    /// Creates the outline of the shape for a border of <paramref name="lineWidth"/>, or null when the annotation
    /// defines no shape.
    /// </summary>
    internal abstract PdfPath? CreateShapePath(float lineWidth);

    /// <summary>
    /// Whether a border is drawn with <paramref name="strokeStyle"/>.
    /// </summary>
    private bool HasBorder(PdfStrokeStyle strokeStyle) => strokeStyle.LineWidth > 0 && Color?.Length != 0;
}
