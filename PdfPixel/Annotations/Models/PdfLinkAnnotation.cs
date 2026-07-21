using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF link annotation.
/// </summary>
/// <remarks>
/// Link annotations represent either hypertext links to destinations elsewhere in the document
/// or actions to be performed. They are typically invisible but may have a border or highlight effect.
/// </remarks>
public class PdfLinkAnnotation : PdfTextMarkupAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLinkAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this link annotation.</param>
    internal PdfLinkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Link)
    {
        IPdfValue? destValue = annotationObject.Dictionary.GetValue(PdfTokens.DestKey);
        Destination = PdfDestination.Parse(destValue, annotationObject.Dictionary.Document);

        PdfDictionary? actionDict = annotationObject.Dictionary.GetDictionary(PdfTokens.AKey);
        Action = PdfAction.FromDictionary(actionDict);

        HighlightMode = annotationObject.Dictionary.GetName(PdfTokens.HighlightModeKey).AsEnum<PdfLinkHighlightMode>();
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <inheritdoc/>
    public override bool IsInteractive => true;

    /// <summary>
    /// Gets the parsed destination that should be displayed when the annotation is activated.
    /// </summary>
    /// <remarks>
    /// This property is null if the link uses an Action instead.
    /// Per PDF spec, a link annotation can have either a Dest entry or an A (action) entry, but not both.
    /// </remarks>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Gets the action dictionary that defines the action to be performed when the annotation is activated.
    /// </summary>
    /// <remarks>
    /// This property is null if the link uses a Destination instead.
    /// Common action types include GoTo, GoToR, URI, Launch, etc.
    /// </remarks>
    public PdfAction? Action { get; }

    /// <summary>
    /// Gets the highlight mode that specifies the visual effect to use when the link is activated.
    /// </summary>
    /// <remarks>
    /// Valid values are:
    /// - None: No highlighting
    /// - Invert: Invert the colors of the annotation rectangle (default)
    /// - Outline: Invert the border of the annotation rectangle
    /// - Push: Display the annotation as if it were being pushed
    /// </remarks>
    public PdfLinkHighlightMode HighlightMode { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (BorderStyle == null || BorderStyle.LineWidth <= 0)
        {
            return false;
        }

        PdfColor color = ResolveColor(page, PdfColors.Black);
        float borderWidth = BorderStyle.LineWidth;

        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color, BorderStyle);

        if (BorderStyle.BorderStyleType == PdfBorderStyleType.Underline)
        {
            float y = Rectangle.Top - (borderWidth / 2);
            PdfPathBuilder linePath = new();
            linePath.MoveTo(Rectangle.Left, y);
            linePath.LineTo(Rectangle.Right, y);
            processor.Process(new DrawPathCommand(linePath.ToPath(), paint));
        }
        else
        {
            PdfRectangle rect = new(
                Rectangle.Left + (borderWidth / 2),
                Rectangle.Top + (borderWidth / 2),
                Rectangle.Right - (borderWidth / 2),
                Rectangle.Bottom - (borderWidth / 2));

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(rect);
            processor.Process(new DrawPathCommand(rectPath.ToPath(), paint));
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this link annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and destination or URI.</returns>
    public override string ToString()
    {
        if (Action is PdfUriAction uriAction && !uriAction.Uri.IsEmpty)
        {
            return $"Link Annotation: {uriAction.Uri}";
        }

        if (Destination != null)
        {
            return "Link Annotation: Destination";
        }

        if (Action != null)
        {
            return $"Link Annotation: {Action.ActionType} Action";
        }

        return "Link Annotation";
    }
}
