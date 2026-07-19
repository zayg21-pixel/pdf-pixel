using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Geometry;
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
    PdfRectangle MediaBox { get; }

    /// <summary>
    /// Resolved CropBox rectangle.
    /// </summary>
    PdfRectangle CropBox { get; }

    /// <summary>
    /// Normalized page rotation in degrees (0, 90, 180, 270).
    /// </summary>
    int Rotation { get; }

    /// <summary>
    /// Gets the annotations for this page, each bound to their containing page.
    /// Resolved during page construction from the /Annots array and inheritable annotations.
    /// </summary>
    IReadOnlyList<PdfPageAnnotation> Annotations { get; }

    /// <summary>
    /// Gets the resolved page label for this page (may be null if not present in the document).
    /// </summary>
    PdfString PageLabel { get; }

    /// <summary>
    /// Render the page content via the command processor.
    /// </summary>
    /// <param name="processor">The command processor to emit drawing commands to.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    void Render(IPdfCommandProcessor processor, PdfRenderingParameters renderingParameters, IPdfExecutionObserver observer);

}
