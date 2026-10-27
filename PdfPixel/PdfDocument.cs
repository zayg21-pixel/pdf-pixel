using Microsoft.Extensions.Logging;
using PdfPixel.Actions;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Cache;
using PdfPixel.Encryption;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Files;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Forms;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Navigation;
using PdfPixel.Objects;
using PdfPixel.OptionalContent;
using PdfPixel.Parsing;
using PdfPixel.Resources;
using PdfPixel.Streams;
using PdfPixel.Tagging;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel;

/// <summary>
/// Represents a parsed PDF document, exposing its pages and providing resource management for PDF processing.
/// </summary>
internal class PdfDocument : IPdfDocumentInternal
{
    private readonly ILogger<PdfDocument> _logger;
    private readonly PdfDocumentObjectCache _objectCache;
    private readonly CMapCache _cMapCache;
    private readonly PdfStreamDecoder _streamDecoder;
    private readonly BufferedStream _stream;
    private readonly PdfTreeReader _treeReader;
    private readonly PdfIccColorSpaceConverter? _outputIntentConverter;
    private readonly PdfColorSpaceResolver _colorSpaceResolver;
    private PdfContentHost? _contentHost;

    /// <summary>
    /// Parses the document in <paramref name="fileStream"/>: its cross-reference table, catalog, pages and
    /// document-level entries.
    /// </summary>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="fontSubstitutor">The font source substituted typefaces are loaded from.</param>
    /// <param name="fileStream">The input stream containing the PDF file data.</param>
    /// <param name="onCredentialRequested">Called when the empty user password does not decrypt the document.</param>
    /// <exception cref="PdfInvalidDocumentException">Thrown if the catalog root is not found.</exception>
    /// <exception cref="PdfAuthenticationException">Thrown if no supplied credential is accepted.</exception>
    public PdfDocument(
        ILoggerFactory loggerFactory,
        IFontSubstitutor fontSubstitutor,
        Stream fileStream,
        PdfCredentialRequestedCallback? onCredentialRequested)
    {
        LoggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PdfDocument>();
        _streamDecoder = new PdfStreamDecoder(loggerFactory);
        FontProvider = new FontProvider(fontSubstitutor, this);
        _objectCache = new PdfDocumentObjectCache(this);
        _colorSpaceResolver = new PdfColorSpaceResolver(this);
        _stream = new BufferedStream(fileStream, PdfStreamDecoder.StreamChunkSize);
        _cMapCache = new CMapCache(_logger);
        _treeReader = new PdfTreeReader(loggerFactory.CreateLogger<PdfTreeReader>());

        Header = PdfFileHeader.FromStream(_stream);
        if (Header.Offset != 0)
        {
            _logger.LogWarning("PDF header found at offset {Offset}; declared offsets are relative to it.", Header.Offset);
        }

        PdfPageExtractor pageExtractor = new(this);
        PdfCrossReference crossReference;
        PdfDictionary catalog;
        try
        {
            crossReference = new PdfCrossReferenceParser(this).Parse();
            _objectCache.Load(crossReference.ObjectIndex);
            Decryptor = crossReference.GetDecryptor(onCredentialRequested);
            catalog = GetCatalog(crossReference);
            Destinations = PdfDestinationResolver.FromCatalog(catalog);
            OutputIntents = GetOutputIntents(catalog);
            _outputIntentConverter = PdfOutputIntent.CreateConverter(OutputIntents);
            Pages = pageExtractor.ExtractPages(catalog);
        }
        catch (PdfInvalidDocumentException ex)
        {
            _logger.LogWarning(ex, "Loading from the cross-reference table failed; starting recovery scan.");
            _objectCache.Reset();
            crossReference = new PdfXrefRecoveryScanner(this).Scan();
            _objectCache.Load(crossReference.ObjectIndex);
            Decryptor = crossReference.GetDecryptor(onCredentialRequested);
            catalog = GetCatalog(crossReference);
            Destinations = PdfDestinationResolver.FromCatalog(catalog);
            OutputIntents = GetOutputIntents(catalog);
            _outputIntentConverter = PdfOutputIntent.CreateConverter(OutputIntents);
            Pages = pageExtractor.ExtractPages(catalog);
        }

        Decryptor?.AuthenticateOnOpen();

        Language = catalog.GetString(PdfTokens.LangKey);
        OptionalContentProperties = new PdfOptionalContentProperties(catalog.GetDictionary(PdfTokens.OCPropertiesKey), Language);
        StructureTree = PdfStructureTree.FromCatalog(catalog, _treeReader);
        EmbeddedFiles = PdfEmbeddedFiles.FromCatalog(catalog, _treeReader);

        Version = GetVersion(catalog);
        Extensions = PdfDeveloperExtension.FromExtensions(catalog.GetDictionary(PdfTokens.ExtensionsKey));
        PdfDictionary? information = crossReference.GetInformation();
        if (information != null)
        {
            Information = new PdfDocumentInformation(information);
        }

        Id = crossReference.Id;

        PdfDictionary? markInformation = catalog.GetDictionary(PdfTokens.MarkInfoKey);
        if (markInformation != null)
        {
            MarkInformation = new PdfMarkInformation(markInformation);
        }

        BaseUri = catalog.GetDictionary(PdfTokens.UriDictionaryKey)?.GetString(PdfTokens.BaseKey);
        PageMode = catalog.GetName(PdfTokens.PageModeKey)?.AsEnum<PdfPageMode>();
        PageLayout = catalog.GetName(PdfTokens.PageLayoutKey)?.AsEnum<PdfPageLayout>();
        NeedsRendering = catalog.GetBoolean(PdfTokens.NeedsRenderingKey);
        AssociatedFiles = PdfFileSpecification.GetFileSpecifications(catalog.GetArray(PdfTokens.AssociatedFilesKey));
        Metadata = PdfMetadata.FromDictionary(catalog);
        OpenAction = PdfOpenAction.FromCatalog(catalog, Destinations);
        Outlines = PdfOutlines.FromCatalog(catalog, Destinations, StructureTree);
        Threads = PdfThreads.FromCatalog(catalog);
        Permissions = Decryptor?.Parameters.UserAccessPermissions;
        InteractiveForm = PdfInteractiveForm.FromCatalog(catalog);
    }

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc />
    public FontProvider FontProvider { get; }

    /// <inheritdoc />
    public CommandCache CommandCache { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<IPdfPageInternal> Pages { get; }

    IReadOnlyList<IPdfPage> IPdfDocument.Pages => Pages;

    /// <inheritdoc />
    public PdfDestinationResolver Destinations { get; }

    /// <inheritdoc />
    public PdfOpenAction? OpenAction { get; }

    /// <inheritdoc />
    public PdfOptionalContentProperties OptionalContentProperties { get; }

    /// <inheritdoc />
    public PdfStructureTree StructureTree { get; }

    /// <inheritdoc />
    public PdfOutlines Outlines { get; }

    /// <inheritdoc />
    public PdfThreads Threads { get; }

    /// <inheritdoc />
    public PdfEmbeddedFiles EmbeddedFiles { get; }

    /// <inheritdoc />
    public PdfFileHeader Header { get; }

    /// <inheritdoc />
    public PdfVersion? Version { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>> Extensions { get; }

    /// <inheritdoc />
    public PdfDocumentInformation? Information { get; }

    /// <inheritdoc />
    public PdfFileIdentifier? Id { get; }

    /// <inheritdoc />
    public PdfString? Language { get; }

    /// <inheritdoc />
    public PdfMarkInformation? MarkInformation { get; }

    /// <inheritdoc />
    public PdfString? BaseUri { get; }

    /// <inheritdoc />
    public PdfPageMode? PageMode { get; }

    /// <inheritdoc />
    public PdfPageLayout? PageLayout { get; }

    /// <inheritdoc />
    public bool? NeedsRendering { get; }

    /// <inheritdoc />
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <inheritdoc />
    public IReadOnlyList<PdfOutputIntent>? OutputIntents { get; }

    /// <inheritdoc />
    public PdfMetadata? Metadata { get; }

    /// <inheritdoc />
    public PdfPermissions? Permissions { get; }

    /// <inheritdoc />
    public PdfInteractiveForm? InteractiveForm { get; }

    /// <inheritdoc />
    public PdfDecryptor? Decryptor { get; }

    PdfTreeReader IPdfDocumentInternal.TreeReader => _treeReader;

    PdfDocumentObjectCache IPdfDocumentInternal.ObjectCache => _objectCache;

    PdfIccColorSpaceConverter? IPdfDocumentInternal.OutputIntentConverter => _outputIntentConverter;

    PdfColorSpaceResolver IPdfDocumentInternal.ColorSpaceResolver => _colorSpaceResolver;

    IPdfContentHost IPdfDocumentInternal.ContentHost
    {
        get
        {
            if (_contentHost == null)
            {
                _contentHost = new PdfContentHost(this, PdfResourceNode.CreateEmpty(this), structuralParents: null);
            }

            return _contentHost;
        }
    }

    CMapCache IPdfDocumentInternal.CMapCache => _cMapCache;

    PdfStreamDecoder IPdfDocumentInternal.StreamDecoder => _streamDecoder;

    BufferedStream IPdfDocumentInternal.Stream => _stream;

    /// <summary>
    /// Resolves the catalog of <paramref name="crossReference"/>.
    /// </summary>
    /// <exception cref="PdfInvalidDocumentException">Thrown if the catalog root is not found.</exception>
    private PdfDictionary GetCatalog(PdfCrossReference crossReference)
    {
        PdfObject? rootObject = crossReference.GetRoot(_objectCache);
        if (rootObject == null)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: catalog root not found.");
        }

        return rootObject.Dictionary;
    }

    private static List<PdfOutputIntent>? GetOutputIntents(PdfDictionary catalog)
        => PdfOutputIntent.FromArray(catalog.GetArray(PdfTokens.OutputIntentsKey));

    /// <summary>
    /// The catalog /Version, or null when absent or malformed.
    /// </summary>
    private static PdfVersion? GetVersion(PdfDictionary catalog)
    {
        PdfString? catalogVersionName = catalog.GetName(PdfTokens.CatalogVersionKey);
        if (catalogVersionName == null)
        {
            return null;
        }

        return PdfVersion.Parse(catalogVersionName.Value.Value.Span);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CommandCache.Dispose();
        _objectCache.Dispose();
        FontProvider.Dispose();
        _stream.Dispose();
    }
}
