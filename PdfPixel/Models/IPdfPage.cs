using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a single PDF page, exposing its geometry, metadata, and rendering operations.
/// </summary>
public interface IPdfPage
{
    /// <summary>
    /// 1-based index of this page within the document.
    /// </summary>
    int PageNumber { get; }

    /// <summary>
    /// Resolved MediaBox rectangle.
    /// </summary>
    SKRect MediaBox { get; }

    /// <summary>
    /// Resolved CropBox rectangle.
    /// </summary>
    SKRect CropBox { get; }

    /// <summary>
    /// Normalized page rotation in degrees (0, 90, 180, 270).
    /// </summary>
    int Rotation { get; }

    /// <summary>
    /// Gets the annotations for this page.
    /// Resolved during page construction from the /Annots array and inheritable annotations.
    /// </summary>
    IReadOnlyList<PdfAnnotationBase> Annotations { get; }

    /// <summary>
    /// Gets the resolved page label for this page (may be null if not present in the document).
    /// </summary>
    PdfString PageLabel { get; }

    /// <summary>
    /// Render the page content via the command processor.
    /// </summary>
    /// <param name="processor">The command processor to emit drawing commands to.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    void Draw(IPdfCommandProcessor processor, IPdfExecutionObserver observer);

    /// <summary>
    /// Render a single annotation via the command processor.
    /// The annotation must belong to this page's <see cref="Annotations"/> collection.
    /// </summary>
    /// <param name="processor">The command processor to emit annotation commands to.</param>
    /// <param name="annotation">The annotation to render; must belong to this page.</param>
    /// <param name="visualStateKind">Visual state to apply to the annotation.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    void RenderAnnotation(
        IPdfCommandProcessor processor,
        PdfAnnotationBase annotation,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfExecutionObserver observer); // TODO: [HIGHT] method call, naming inconsistency!

    /// <summary>
    /// Extract text content from the page.
    /// </summary>
    List<PdfCharacter> ExtractText();
}
