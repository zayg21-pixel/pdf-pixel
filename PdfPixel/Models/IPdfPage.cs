using PdfPixel.Annotations.Model;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Files;
using PdfPixel.Imaging.Model;
using System;
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
    /// Gets the resolved page label for this page (may be null if not present in the document).
    /// </summary>
    PdfString PageLabel { get; }

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
    /// Size of a default user space unit in multiples of 1/72 inch (/UserUnit); 1 when absent.
    /// </summary>
    float UserUnit { get; }

    /// <summary>
    /// Resolved BleedBox rectangle: defaults to <see cref="CropBox"/>, intersected with <see cref="MediaBox"/>.
    /// </summary>
    PdfRectangle BleedBox { get; }

    /// <summary>
    /// Resolved TrimBox rectangle: defaults to <see cref="CropBox"/>, intersected with <see cref="MediaBox"/>.
    /// </summary>
    PdfRectangle TrimBox { get; }

    /// <summary>
    /// Resolved ArtBox rectangle: defaults to <see cref="CropBox"/>, intersected with <see cref="MediaBox"/>.
    /// </summary>
    PdfRectangle ArtBox { get; }

    /// <summary>
    /// Gets the annotations for this page, each bound to their containing page.
    /// Resolved during page construction from the /Annots array and inheritable annotations.
    /// </summary>
    IReadOnlyList<PdfPageAnnotation> Annotations { get; }

    /// <summary>
    /// Thumbnail image of the page (/Thumb), or <see langword="null"/> when absent.
    /// </summary>
    PdfImage? Thumbnail { get; }

    /// <summary>
    /// Page metadata stream (/Metadata), or <see langword="null"/> when absent.
    /// </summary>
    PdfMetadata? Metadata { get; }

    /// <summary>
    /// Files associated with the page (/AF), or <see langword="null"/> when absent.
    /// </summary>
    IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// Date the page contents were most recently modified (/LastModified), or <see langword="null"/> when absent.
    /// </summary>
    DateTime? LastModified { get; }

    /// <summary>
    /// Key of the page's entry in the structural parent tree (/StructParents), or <see langword="null"/> when absent.
    /// </summary>
    int? StructParents { get; }

    /// <summary>
    /// Tab order used for annotations on the page (/Tabs), or <see langword="null"/> when absent.
    /// </summary>
    PdfTabOrder? Tabs { get; }

    /// <summary>
    /// Maximum number of seconds the page is displayed during presentations before advancing (/Dur),
    /// or <see langword="null"/> when the page does not advance automatically.
    /// </summary>
    float? Duration { get; }

    /// <summary>
    /// Name of the named page the page was created from (/TemplateInstantiated), or <see langword="null"/> when absent.
    /// </summary>
    PdfString? TemplateInstantiated { get; }

    /// <summary>
    /// Digital identifier of the page's parent Web Capture content set (/ID), or <see langword="null"/> when absent.
    /// </summary>
    PdfString? Id { get; }

    /// <summary>
    /// Preferred zoom factor of the page (/PZ), or <see langword="null"/> when absent.
    /// </summary>
    float? PreferredZoom { get; }

    /// <summary>
    /// Render the page content via the command processor.
    /// </summary>
    /// <param name="processor">The command processor to emit drawing commands to.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    void Render(IPdfCommandProcessor processor, PdfRenderingParameters renderingParameters, IPdfExecutionObserver observer);

}
