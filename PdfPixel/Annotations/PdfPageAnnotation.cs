using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations;

/// <summary>
/// An annotation of a page.
/// </summary>
public sealed class PdfPageAnnotation
{
    private readonly IPdfPageInternal _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageAnnotation"/> class.
    /// </summary>
    internal PdfPageAnnotation(IPdfPageInternal page, PdfAnnotation annotation)
    {
        _page = page;
        Content = annotation;
    }

    /// <summary>
    /// Gets the page the annotation belongs to.
    /// </summary>
    public IPdfPage Page => _page;

    /// <summary>
    /// Gets the underlying annotation.
    /// </summary>
    public PdfAnnotation Content { get; }

    /// <summary>
    /// Returns the hover rectangles for this annotation in PDF page coordinates.
    /// </summary>
    public PdfRectangle[] GetHoverRectangles() => Content.GetHoverRectangles(_page);

    /// <summary>
    /// Returns the rectangle of the content bubble indicator placed at <paramref name="anchor"/>, in PDF page coordinates.
    /// </summary>
    /// <param name="anchor">
    /// The point the bubble is placed at, such as <see cref="PdfAnnotation.ContentStart"/>.
    /// </param>
    public PdfRectangle GetBubbleRectangle(in PdfPoint anchor) => PdfAnnotation.GetBubbleRectangle(_page, anchor);

    /// <summary>
    /// Renders this annotation via the command processor.
    /// </summary>
    /// <param name="processor">
    /// The command processor to emit drawing commands to.
    /// </param>
    /// <param name="visualStateKind">
    /// Visual state to apply to the annotation.
    /// </param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">
    /// Execution observer to notify on long-running operations.
    /// </param>
    public void Render(
        IPdfCommandProcessor processor,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfRenderer renderer = new(_page.Document.LoggerFactory);
        Content.Render(processor, _page, visualStateKind, renderer, renderingParameters, observer);
    }

    /// <summary>
    /// Renders the content bubble indicator of this annotation via the command processor.
    /// </summary>
    /// <param name="processor">
    /// The command processor to emit drawing commands to.
    /// </param>
    /// <param name="anchor">
    /// The point the bubble is placed at, such as <see cref="PdfAnnotation.ContentStart"/>.
    /// </param>
    /// <param name="visualStateKind">
    /// Visual state to apply to the bubble.
    /// </param>
    public void RenderBubble(IPdfCommandProcessor processor, in PdfPoint anchor, PdfAnnotationVisualStateKind visualStateKind)
        => Content.RenderBubble(processor, _page, anchor, visualStateKind);
}
