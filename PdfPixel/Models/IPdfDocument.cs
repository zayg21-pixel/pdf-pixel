using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Cache;
using PdfPixel.Files;
using PdfPixel.Fonts.Management;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Tagging.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF document.
/// </summary>
public interface IPdfDocument : IDisposable
{
    /// <summary>
    /// Gets the list of pages in the PDF document.
    /// </summary>
    IReadOnlyList<IPdfPage> Pages { get; }

    /// <summary>
    /// Gets the version the document conforms to: the catalog /Version when later than the header version,
    /// otherwise the header version; <see langword="null"/> when neither is readable.
    /// </summary>
    PdfVersion? Version { get; }

    /// <summary>
    /// Gets the version declared by the <c>%PDF-</c> file header, or <see langword="null"/> when it is malformed.
    /// </summary>
    PdfVersion? HeaderVersion { get; }

    /// <summary>
    /// Gets the document information dictionary (trailer /Info), or <see langword="null"/> when absent.
    /// </summary>
    PdfDocumentInformation? Information { get; }

    /// <summary>
    /// Gets the document metadata stream (/Metadata), or <see langword="null"/> when absent.
    /// </summary>
    PdfMetadata? Metadata { get; }

    /// <summary>
    /// Gets the user access permissions (encryption /P), or <see langword="null"/> when the document is not encrypted.
    /// </summary>
    PdfPermissions? Permissions { get; }

    /// <summary>
    /// Gets how the document is displayed when opened (/PageMode), or <see langword="null"/> when absent.
    /// </summary>
    PdfPageMode? PageMode { get; }

    /// <summary>
    /// Gets the page layout used when the document is opened (/PageLayout), or <see langword="null"/> when absent.
    /// </summary>
    PdfPageLayout? PageLayout { get; }

    /// <summary>
    /// Gets the natural language of the document (/Lang), or <see langword="null"/> when absent.
    /// </summary>
    PdfString? Lang { get; }

    /// <summary>
    /// Gets the document-level attachments (/Names /EmbeddedFiles), keyed by their name.
    /// Empty if the document has none.
    /// </summary>
    IReadOnlyDictionary<PdfString, PdfFileSpecification> EmbeddedFiles { get; }

    /// <summary>
    /// Gets the files associated with the document as a whole (/AF), or <see langword="null"/> when absent.
    /// </summary>
    IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// Gets the optional content (layers) properties (/OCProperties), or <see langword="null"/> when the
    /// document has no optional content.
    /// </summary>
    PdfOptionalContentProperties? OptionalContentProperties { get; }

    /// <summary>
    /// Gets the document structure tree providing per-page MCID reading order,
    /// or <see langword="null"/> for untagged documents.
    /// </summary>
    PdfStructureTree? StructureTree { get; }

    /// <summary>
    /// Gets the mark information dictionary (/MarkInfo), or <see langword="null"/> when absent.
    /// </summary>
    PdfMarkInformation? MarkInformation { get; }

    /// <summary>
    /// Gets the document-level signature field characteristics (/AcroForm /SigFlags).
    /// </summary>
    PdfSignatureFlags SignatureFlags { get; }

    /// <summary>
    /// Gets whether the document's XFA form must be rendered by the viewer (/NeedsRendering),
    /// or <see langword="null"/> when absent.
    /// </summary>
    bool? NeedsRendering { get; }

    /// <summary>
    /// Gets the base URI for resolving relative URI references (/URI /Base), or <see langword="null"/> when absent.
    /// </summary>
    PdfString? BaseUri { get; }

    /// <summary>
    /// Gets the file identifier (trailer /ID), or <see langword="null"/> when absent.
    /// </summary>
    PdfFileIdentifier? Id { get; }

    /// <summary>
    /// Gets the developer extensions (/Extensions) the document uses, keyed by developer prefix name.
    /// Empty if the document declares none.
    /// </summary>
    IReadOnlyDictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>> Extensions { get; }

    /// <summary>
    /// Gets the document-level font provider.
    /// </summary>
    FontProvider FontProvider { get; }

    /// <summary>
    /// Gets the cache holding values built during command execution that stay valid for the lifetime
    /// of the document, so every replay of any of its pages reuses the same entries.
    /// </summary>
    CommandCache CommandCache { get; }

    /// <summary>
    /// Gets the logger factory used for creating loggers.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }
}
