using PdfPixel.Annotations.Icons;
using PdfPixel.Annotations.Parsing;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents a PDF caret annotation, indicating a text insertion point.
/// </summary>
public class PdfCaretAnnotation : PdfMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCaretAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this caret annotation.</param>
    internal PdfCaretAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Caret)
    {
        Symbol = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.SymbolKey).AsEnum<PdfCaretSymbol>();

        RectangleDifferences = PdfAnnotationGeometryParser.ParseInsets(annotationObject.Dictionary.GetArray(PdfTokens.RectDifferencesKey));
        MarkRectangle = ApplyRectDifferences(Rectangle, RectangleDifferences);
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Gets the symbol associated with this caret annotation.
    /// </summary>
    public PdfCaretSymbol Symbol { get; }

    /// <summary>
    /// Gets the rectangle differences that inset the actual caret mark from the annotation rectangle.
    /// </summary>
    public PdfRectangle? RectangleDifferences { get; }

    /// <summary>
    /// Gets the actual mark rectangle after applying <see cref="RectangleDifferences"/>.
    /// </summary>
    public PdfRectangle MarkRectangle { get; }

    internal override bool RenderFallback(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfAnnotationIconDefinition? icon = PdfAnnotationGraphics.GetAnnotationIcon(Symbol.ToString(), visualStateKind);

        if (icon == null)
        {
            return false;
        }

        PdfColor color = ResolveColor(page);
        PdfAnnotationGraphics.RenderIcon(processor, icon, MarkRectangle, color, color);
        return true;
    }
}
