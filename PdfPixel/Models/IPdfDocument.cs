using Microsoft.Extensions.Logging;
using PdfPixel.TextExtraction;
using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF document, exposing its pages and metadata.
/// </summary>
public interface IPdfDocument : IDisposable
{
    /// <summary>
    /// Gets the logger factory used for creating loggers.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the list of pages in the PDF document.
    /// </summary>
    IReadOnlyList<IPdfPage> Pages { get; }

    /// <summary>
    /// Gets the optional content groups (layers) defined in the document,
    /// keyed by their indirect object reference. Empty if the document has no optional content.
    /// </summary>
    IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> OptionalContentGroups { get; }

    /// <summary>
    /// Gets the document structure tree providing per-page MCID reading order,
    /// or <see langword="null"/> for untagged documents.
    /// </summary>
    PdfStructureTree? StructureTree { get; }
}
