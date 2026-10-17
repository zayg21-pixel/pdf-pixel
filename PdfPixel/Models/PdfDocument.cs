using Microsoft.Extensions.Logging;
using PdfPixel.Actions.Model;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Intent;
using PdfPixel.Commands.Cache;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Encryption.Model;
using PdfPixel.Files;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Navigation;
using PdfPixel.Navigation.Model;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System;
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
        FontProvider = new FontProvider(fontSubstitutor, loggerFactory);
        _objectCache = new PdfDocumentObjectCache(this, new PdfObjectParser(this));
        _colorSpaceResolver = new PdfColorSpaceResolver(this);
        _stream = new BufferedStream(fileStream, PdfStreamDecoder.StreamChunkSize);
        _cMapCache = new CMapCache(_logger);
        _treeReader = new PdfTreeReader(loggerFactory.CreateLogger<PdfTreeReader>());
        Destinations = new PdfDestinationResolver(this);

        IPdfDocumentInternal internalDocument = this;
        internalDocument.OnCredentialRequested = onCredentialRequested;

        PdfDictionary catalog = LoadCatalog();
        PdfDictionary? trailer = internalDocument.Trailer;

        List<PdfOutputIntent>? outputIntents = PdfOutputIntent.FromArray(catalog.GetArray(PdfTokens.OutputIntentsKey));
        OutputIntents = outputIntents;
        _outputIntentConverter = PdfOutputIntent.CreateConverter(outputIntents);

        new PdfPageExtractor(this).ExtractPages();
        internalDocument.Decryptor?.AuthenticateOnOpen();

        Lang = catalog.GetString(PdfTokens.LangKey);
        OptionalContentProperties = new PdfOptionalContentProperties(catalog.GetDictionary(PdfTokens.OCPropertiesKey), Lang);
        StructureTree = PdfStructureTree.FromCatalog(catalog, _treeReader);
        EmbeddedFiles = PdfEmbeddedFiles.FromCatalog(catalog, _treeReader);

        HeaderVersion = ReadHeaderVersion();
        Version = GetVersion(HeaderVersion, catalog);
        Extensions = PdfDeveloperExtension.FromExtensions(catalog.GetDictionary(PdfTokens.ExtensionsKey));
        PdfDictionary? information = trailer?.GetDictionary(PdfTokens.InfoKey);
        if (information != null)
        {
            Information = new PdfDocumentInformation(information);
        }

        Id = PdfFileIdentifier.FromArray(trailer?.GetArray(PdfTokens.IdKey));

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
        Permissions = GetPermissions(internalDocument.Decryptor?.Parameters.Permissions);
        SignatureFlags = GetSignatureFlags(catalog);
    }

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc />
    public FontProvider FontProvider { get; }

    /// <inheritdoc />
    public CommandCache CommandCache { get; } = new();

    IReadOnlyList<IPdfPage> IPdfDocument.Pages => _pages;

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
    public PdfVersion? HeaderVersion { get; }

    /// <inheritdoc />
    public PdfVersion? Version { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>> Extensions { get; }

    /// <inheritdoc />
    public PdfDocumentInformation? Information { get; }

    /// <inheritdoc />
    public PdfFileIdentifier? Id { get; }

    /// <inheritdoc />
    public PdfString? Lang { get; }

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
    public PdfSignatureFlags SignatureFlags { get; }

    List<IPdfPageInternal> IPdfDocumentInternal.Pages => _pages;

    PdfTreeReader IPdfDocumentInternal.TreeReader => _treeReader;

    PdfObject? IPdfDocumentInternal.RootObject { get; set; }

    PdfDictionary? IPdfDocumentInternal.Trailer { get; set; }

    int IPdfDocumentInternal.HeaderOffset { get; set; }

    BasePdfDecryptor? IPdfDocumentInternal.Decryptor { get; set; }

    PdfCredentialRequestedCallback? IPdfDocumentInternal.OnCredentialRequested { get; set; }

    PdfDocumentObjectCache IPdfDocumentInternal.ObjectCache => _objectCache;

    PdfIccColorSpaceConverter? IPdfDocumentInternal.OutputIntentConverter => _outputIntentConverter;

    PdfColorSpaceResolver IPdfDocumentInternal.ColorSpaceResolver => _colorSpaceResolver;

    IPdfContentHost IPdfDocumentInternal.ContentHost
    {
        get
        {
            if (_contentHost == null)
            {
                _contentHost = new PdfContentHost(this, PdfResourceNode.CreateEmpty(this), structParents: null);
            }

            return _contentHost;
        }
    }

    CMapCache IPdfDocumentInternal.CMapCache => _cMapCache;

    PdfStreamDecoder IPdfDocumentInternal.StreamDecoder => _streamDecoder;

    BufferedStream IPdfDocumentInternal.Stream => _stream;

    /// <summary>
    /// Locates the header, loads the cross-reference table, falling back to a recovery scan, and returns the catalog.
    /// </summary>
    private PdfDictionary LoadCatalog()
    {
        IPdfDocumentInternal internalDocument = this;
        internalDocument.HeaderOffset = PdfByteScanner.LocateHeader(_stream);

        if (internalDocument.HeaderOffset != 0)
        {
            _logger.LogWarning("PDF header found at offset {Offset}; declared offsets are relative to it.", internalDocument.HeaderOffset);
        }

        try
        {
            new PdfXrefLoader(this).LoadXref();
        }
        catch (PdfInvalidDocumentException ex)
        {
            _logger.LogWarning(ex, "Xref loading failed; attempting recovery scan.");
        }

        if (internalDocument.RootObject == null)
        {
            _logger.LogInformation("Xref incomplete (no catalog root); starting recovery scan.");
            _objectCache.RunRecoveryScan();
        }

        if (internalDocument.RootObject == null)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: catalog root not found.");
        }

        return internalDocument.RootObject.Dictionary;
    }

    private PdfVersion? ReadHeaderVersion()
    {
        long versionPosition = ((IPdfDocumentInternal)this).HeaderOffset + PdfTokens.Header.Length;
        if (versionPosition >= _stream.Length)
        {
            return null;
        }

        var versionBytes = new byte[8];
        long previousPosition = _stream.Position;
        _stream.Position = versionPosition;
        int bytesRead = _stream.Read(versionBytes, 0, versionBytes.Length);
        _stream.Position = previousPosition;

        return PdfVersion.Parse(versionBytes.AsSpan(0, bytesRead));
    }

    /// <summary>
    /// The catalog /Version when later than <paramref name="headerVersion"/>, otherwise <paramref name="headerVersion"/>.
    /// </summary>
    private static PdfVersion? GetVersion(PdfVersion? headerVersion, PdfDictionary catalog)
    {
        PdfString? catalogVersionName = catalog.GetName(PdfTokens.CatalogVersionKey);
        PdfVersion? catalogVersion = (catalogVersionName == null) ? null : PdfVersion.Parse(catalogVersionName.Value.Value.Span);

        if (catalogVersion != null && (headerVersion == null || catalogVersion.Value.IsLaterThan(headerVersion.Value)))
        {
            return catalogVersion;
        }

        return headerVersion;
    }

    private static PdfPermissions? GetPermissions(int? permissions)
    {
        if (permissions == null)
        {
            return null;
        }

        PdfPermissions definedPermissions = PdfPermissions.Print
            | PdfPermissions.Modify
            | PdfPermissions.Copy
            | PdfPermissions.Annotate
            | PdfPermissions.FillForms
            | PdfPermissions.Assemble
            | PdfPermissions.HighQualityPrint;

        return (PdfPermissions)permissions.Value & definedPermissions;
    }

    private static PdfSignatureFlags GetSignatureFlags(PdfDictionary catalog)
    {
        int signatureFlags = catalog.GetDictionary(PdfTokens.AcroFormKey)?.GetInteger(PdfTokens.SigFlagsKey) ?? 0;

        return (PdfSignatureFlags)signatureFlags & (PdfSignatureFlags.SignaturesExist | PdfSignatureFlags.AppendOnly);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        //foreach (System.Collections.Generic.KeyValuePair<PdfReference, int> probeEntry in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(PdfPixel.Parsing.PdfObjectParser.ProbeParseCounts, entry => entry.Value), 8))
        //{
        //    System.Console.WriteLine($"PROBE {probeEntry.Key} parsed {probeEntry.Value}x");
        //}

        //foreach (System.Collections.Generic.KeyValuePair<string, int> probeEntry in System.Linq.Enumerable.OrderByDescending(PdfPixel.Parsing.PdfObjectParser.ProbeRepeatCallers, entry => entry.Value))
        //{
        //    System.Console.WriteLine($"PROBE repeat caller {probeEntry.Key} {probeEntry.Value}x");
        //}

        //System.Console.WriteLine($"PROBE distinct {PdfPixel.Parsing.PdfObjectParser.ProbeParseCounts.Count}, total {System.Linq.Enumerable.Sum(PdfPixel.Parsing.PdfObjectParser.ProbeParseCounts.Values)}");
        PdfPixel.Parsing.PdfObjectParser.ProbeParseCounts.Clear();
        PdfPixel.Parsing.PdfObjectParser.ProbeRepeatCallers.Clear();
        CommandCache.Dispose();
        _objectCache.Dispose();
        FontProvider.Dispose();
        _stream.Dispose();
    }
}
