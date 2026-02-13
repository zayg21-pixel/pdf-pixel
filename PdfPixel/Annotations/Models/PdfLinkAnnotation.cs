using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

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
    public PdfLinkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Link)
    {
        var destValue = annotationObject.Dictionary.GetValue(PdfTokens.DestKey);
        Destination = PdfDestination.Parse(destValue, annotationObject.Dictionary.Document);

        var actionDict = annotationObject.Dictionary.GetDictionary(PdfTokens.AKey);
        Action = PdfAction.FromDictionary(actionDict);

        HighlightMode = annotationObject.Dictionary.GetName(PdfTokens.HighlightModeKey).AsEnum<PdfLinkHighlightMode>();
    }

    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Gets the parsed destination that should be displayed when the annotation is activated.
    /// </summary>
    /// <remarks>
    /// This property is null if the link uses an Action instead.
    /// Per PDF spec, a link annotation can have either a Dest entry or an A (action) entry, but not both.
    /// </remarks>
    public PdfDestination Destination { get; }

    /// <summary>
    /// Gets the action dictionary that defines the action to be performed when the annotation is activated.
    /// </summary>
    /// <remarks>
    /// This property is null if the link uses a Destination instead.
    /// Common action types include GoTo, GoToR, URI, Launch, etc.
    /// </remarks>
    public PdfAction Action { get; }

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

    /// <summary>
    /// Creates a fallback rendering for link annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered link border, or null if the link is invisible.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (BorderStyle == null || BorderStyle.Width <= 0)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var color = ResolveColor(page, SKColors.Black);
        var borderWidth = BorderStyle.Width;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            Color = color,
            IsAntialias = true
        };

        var needsNormalDraw = BorderStyle.TryApplyEffect(paint, color);

        if (!needsNormalDraw && BorderStyle.Style == PdfBorderStyleType.Underline)
        {
            var y = Rectangle.Top - borderWidth / 2;
            canvas.DrawLine(Rectangle.Left, y, Rectangle.Right, y, paint);
        }
        else
        {
            var rect = new SKRect(
                Rectangle.Left + borderWidth / 2,
                Rectangle.Top + borderWidth / 2,
                Rectangle.Right - borderWidth / 2,
                Rectangle.Bottom - borderWidth / 2);

            canvas.DrawRect(rect, paint);
        }

        return recorder.EndRecording();
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
