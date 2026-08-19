using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Binds a <see cref="PdfAnnotationBase"/> to the page it belongs to, providing page-aware
/// rendering and hit-testing without exposing the internal page reference to callers.
/// </summary>
public sealed class PdfPageAnnotation
{
    private readonly IPdfPageInternal _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageAnnotation"/> class.
    /// </summary>
    internal PdfPageAnnotation(IPdfPageInternal page, PdfAnnotationBase annotation)
    {
        _page = page;
        Content = annotation;
    }

    /// <summary>
    /// Gets the underlying annotation.
    /// </summary>
    public PdfAnnotationBase Content { get; }

    /// <summary>
    /// Returns the hover rectangle for this annotation in PDF page coordinates.
    /// </summary>
    public PdfRectangle GetHoverRectangle() => Content.GetHoverRectangle(_page);

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
}
