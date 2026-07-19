using PdfPixel.Annotations.Rendering;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF caret annotation, indicating a text insertion point.
/// </summary>
/// <remarks>
/// Caret annotations mark a position in a PDF document where text should be inserted.
/// The <see cref="Symbol"/> entry indicates whether a paragraph break is implied.
/// The <see cref="RectangleDifferences"/> entry insets the actual mark from the annotation rectangle.
/// </remarks>
public class PdfCaretAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCaretAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this caret annotation.</param>
    public PdfCaretAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Caret)
    {
        Symbol = annotationObject.Dictionary.GetName(PdfTokens.SymbolKey).AsEnum<PdfCaretSymbol>();

        RectangleDifferences = PdfRectangle.FromArray(annotationObject.Dictionary.GetArray(PdfTokens.RectDifferencesKey));
        MarkRectangle = ApplyRectDifferences(Rectangle, RectangleDifferences);
    }

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

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        PdfAnnotationIconDefinition? icon = PdfAnnotationGraphics.GetAnnotationIcon(Symbol.ToString(), visualStateKind);

        if (icon == null)
        {
            return false;
        }

        SKColor color = ResolveColor(page, SKColors.DarkBlue);
        PdfAnnotationGraphics.RenderIcon(processor, icon, MarkRectangle.ToSkRect(), color, null);
        return true;
    }
}
