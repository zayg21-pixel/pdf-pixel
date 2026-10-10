using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Cache;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Encryption.Model;
using PdfPixel.Files;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using PdfPixel.Tagging.Model;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Models;

/// <summary>
/// Represents a parsed PDF document, exposing its pages and providing resource management for PDF processing.
/// </summary>
internal class PdfDocument : IPdfDocumentInternal
{
    private readonly ILogger<PdfDocument> _logger;
    private readonly List<IPdfPageInternal> _pages = [];
    private readonly FontProvider _fontProvider;
    private readonly PdfDocumentObjectCache _objectCache;
    private readonly CMapCache _cMapCache;
    private readonly PdfStreamDecoder _streamDecoder;
    private readonly BufferedStream _stream;
    private readonly PdfDestinationResolver _destinationResolver;
    private readonly PdfTreeReader _treeReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDocument"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="fontSubstitutor">The font source substituted typefaces are loaded from.</param>
    /// <param name="fileStream">The input stream containing the PDF file data.</param>
    public PdfDocument(ILoggerFactory loggerFactory, IFontSubstitutor fontSubstitutor, Stream fileStream)
    {
        LoggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfDocument>();
        _streamDecoder = new PdfStreamDecoder(loggerFactory);
        _fontProvider = new FontProvider(fontSubstitutor, loggerFactory);
        _objectCache = new PdfDocumentObjectCache(this, new PdfObjectParser(this));
        _stream = new BufferedStream(fileStream);
        _cMapCache = new CMapCache(_logger);
        _treeReader = new PdfTreeReader(loggerFactory.CreateLogger<PdfTreeReader>());
        _destinationResolver = new PdfDestinationResolver(this);
    }

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc />
    public FontProvider FontProvider => _fontProvider;

    /// <inheritdoc />
    public CommandCache CommandCache { get; } = new();

    IReadOnlyList<IPdfPage> IPdfDocument.Pages => _pages;

    /// <inheritdoc />
    public IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> OptionalContentGroups { get; internal set; } = new Dictionary<PdfReference, PdfOptionalContentGroup>();

    /// <inheritdoc />
    public PdfStructureTree? StructureTree { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<PdfString, PdfFileSpecification> EmbeddedFiles { get; internal set; } = new Dictionary<PdfString, PdfFileSpecification>();

    /// <inheritdoc />
    public PdfVersion? HeaderVersion { get; internal set; }

    /// <inheritdoc />
    public PdfVersion? Version { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>> Extensions { get; internal set; } = new Dictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>>();

    /// <inheritdoc />
    public PdfDocumentInformation? Information { get; internal set; }

    /// <inheritdoc />
    public PdfFileIdentifier? Id { get; internal set; }

    /// <inheritdoc />
    public PdfString? Lang { get; internal set; }

    /// <inheritdoc />
    public PdfMarkInformation? MarkInformation { get; internal set; }

    /// <inheritdoc />
    public PdfString? BaseUri { get; internal set; }

    /// <inheritdoc />
    public PdfPageMode? PageMode { get; internal set; }

    /// <inheritdoc />
    public PdfPageLayout? PageLayout { get; internal set; }

    /// <inheritdoc />
    public bool? NeedsRendering { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; internal set; }

    /// <inheritdoc />
    public PdfMetadata? Metadata { get; internal set; }

    /// <inheritdoc />
    public PdfPermissions? Permissions { get; internal set; }

    /// <inheritdoc />
    public PdfSignatureFlags SignatureFlags { get; internal set; }

    List<IPdfPageInternal> IPdfDocumentInternal.Pages => _pages;

    PdfDestinationResolver IPdfDocumentInternal.Destinations => _destinationResolver;

    PdfTreeReader IPdfDocumentInternal.TreeReader => _treeReader;

    PdfObject? IPdfDocumentInternal.RootObject { get; set; }

    PdfDictionary? IPdfDocumentInternal.Trailer { get; set; }

    int IPdfDocumentInternal.HeaderOffset { get; set; }

    BasePdfDecryptor? IPdfDocumentInternal.Decryptor { get; set; }

    PdfCredentialRequestedCallback? IPdfDocumentInternal.OnCredentialRequested { get; set; }

    PdfDocumentObjectCache IPdfDocumentInternal.ObjectCache => _objectCache;

    CMapCache IPdfDocumentInternal.CMapCache => _cMapCache;

    PdfStreamDecoder IPdfDocumentInternal.StreamDecoder => _streamDecoder;

    BufferedStream IPdfDocumentInternal.Stream => _stream;

    /// <inheritdoc/>
    public void Dispose()
    {
        CommandCache.Dispose();

        foreach (IPdfTypeface typeface in _objectCache.Typefaces.Values)
        {
            typeface.Dispose();
        }

        _fontProvider.Dispose();
        _stream.Dispose();
    }
}
