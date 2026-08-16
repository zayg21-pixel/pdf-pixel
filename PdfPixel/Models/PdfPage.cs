using PdfPixel.Rendering;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Streams;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using System;
using System.Collections.Generic;
using PdfPixel.Geometry;

namespace PdfPixel.Models;

/// <summary>
/// Represents a single PDF page with its resolved geometry, resources, and underlying /Page object.
/// All geometry (MediaBox, CropBox, Rotation) and the resource dictionary are resolved beforehand
/// by <see cref="Parsing.PdfPageExtractor"/>. This class is a pure data model with minimal logic.
/// </summary>
internal class PdfPage : IPdfPageInternal
{
    private static readonly PdfRectangle DefaultMediaBox = new(0, 0, 612, 792);

    private readonly Lazy<PdfPageCache> _pageCache;
    private readonly Lazy<IReadOnlyList<PdfPageAnnotation>> _annotations;
    private readonly IPdfDocumentInternal _document;
    private readonly PdfReference _pageReference;
    private readonly List<PdfObjectStream> _contentStreams;
    private readonly PdfPageResources _pageResources;
    private readonly PdfDictionary _resourceDictionary;
    private readonly PdfTransparencyGroup? _transparencyGroup;

    /// <summary>
    /// Initializes a new instance from values already resolved out of the page object, so that a caller
    /// holding a page's reference, content streams and resources does not have to materialize the object.
    /// </summary>
    /// <param name="pageNumber">1-based page index.</param>
    /// <param name="pageLabel">Resolved page label for this page.</param>
    /// <param name="document">Owning document.</param>
    /// <param name="pageReference">Reference of the underlying page object.</param>
    /// <param name="contentStreams">Content streams making up the page.</param>
    /// <param name="pageResources">Resolved inheritable page resources snapshot.</param>
    /// <param name="resourceDictionary">Resource dictionary the page's names resolve against.</param>
    /// <param name="groupOwnerDictionary">Dictionary holding the page's /Group entry, or null when there is none.</param>
    protected internal PdfPage(
        int pageNumber,
        in PdfString pageLabel,
        IPdfDocumentInternal document,
        in PdfReference pageReference,
        List<PdfObjectStream> contentStreams,
        PdfPageResources pageResources,
        PdfDictionary resourceDictionary,
        PdfDictionary? groupOwnerDictionary)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _pageReference = pageReference;
        _contentStreams = contentStreams ?? throw new ArgumentNullException(nameof(contentStreams));
        _pageResources = pageResources ?? throw new ArgumentNullException(nameof(pageResources));

        PageNumber = pageNumber;
        PdfRectangle media = pageResources.MediaBoxRect ?? DefaultMediaBox;
        PdfRectangle crop = pageResources.CropBoxRect ?? media;
        crop = PdfRectangle.Intersect(crop, media);
        MediaBox = media;
        CropBox = crop;
        Rotation = pageResources.Rotate ?? 0;
        _resourceDictionary = resourceDictionary;
        PageLabel = pageLabel;
        _pageCache = new Lazy<PdfPageCache>(() => new PdfPageCache(this, document, resourceDictionary));

        _annotations = new Lazy<IReadOnlyList<PdfPageAnnotation>>(CreateAnnotations);

        _transparencyGroup = PdfSoftMaskParser.ParseTransparencyGroup(groupOwnerDictionary, PdfTokens.GroupKey, this);
    }

    protected internal PdfPage(
        int pageNumber,
        in PdfString pageLabel,
        IPdfDocumentInternal document,
        PdfObject pageObject,
        PdfPageResources pageResources,
        PdfDictionary resourceDictionary)
        : this(
            pageNumber,
            pageLabel,
            document,
            (pageObject ?? throw new ArgumentNullException(nameof(pageObject))).Reference,
            ExtractContentStreams(pageObject),
            pageResources,
            resourceDictionary,
            pageObject.Dictionary)
    {
    }

    /// <summary>
    /// Initializes a new instance using <see cref="PdfPageResources"/> snapshot (rotation already normalized there).
    /// </summary>
    /// <param name="pageNumber">1-based page index.</param>
    /// <param name="pageLabel">Resolved page label for this page.</param>
    /// <param name="document">Owning document.</param>
    /// <param name="pageObject">Underlying /Page object.</param>
    /// <param name="pageResources">Resolved inheritable page resources snapshot.</param>
    internal PdfPage(
        int pageNumber,
        in PdfString pageLabel,
        IPdfDocumentInternal document,
        PdfObject pageObject,
        PdfPageResources pageResources)
        : this(pageNumber, pageLabel, document, pageObject, pageResources, pageResources.Resources ?? new PdfDictionary(document))
    {
    }

    /// <inheritdoc/>
    public int PageNumber { get; }

    /// <inheritdoc/>
    public PdfRectangle MediaBox { get; }

    /// <inheritdoc/>
    public PdfRectangle CropBox { get; }

    /// <inheritdoc/>
    public int Rotation { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PdfPageAnnotation> Annotations => _annotations.Value;

    /// <inheritdoc/>
    public PdfString PageLabel { get; }

    PdfPageCache IPdfPageInternal.Cache => _pageCache.Value;

    PdfPageResources IPdfPageInternal.PageResources => _pageResources;

    PdfReference IPdfPageInternal.PageReference => _pageReference;

    IReadOnlyList<PdfObjectStream> IPdfPageInternal.ContentStreams => _contentStreams;

    PdfDictionary IPdfPageInternal.ResourceDictionary => _resourceDictionary;

    IPdfDocumentInternal IPdfPageInternal.Document => _document;

    PdfTransparencyGroup? IPdfPageInternal.TransparencyGroup => _transparencyGroup;

    /// <inheritdoc/>
    public void Render(IPdfCommandProcessor processor, PdfRenderingParameters renderingParameters, IPdfExecutionObserver observer)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (renderingParameters == null)
        {
            throw new ArgumentNullException(nameof(renderingParameters));
        }

        if (_document == null)
        {
            throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
        }

        PdfRenderer renderer = new(_document.LoggerFactory);
        PdfContentStreamRenderer contentRenderer = new(renderer, this);

        if (_transparencyGroup != null)
        {
            processor.Process(new SaveLayerCommand(CropBox));
        }

        contentRenderer.RenderContent(processor, renderingParameters, observer);

        if (_transparencyGroup != null)
        {
            processor.Process(RestoreLayerCommand.Instance);
        }
    }

    /// <summary>
    /// Binds each annotation resolved for this page to the page itself.
    /// </summary>
    private List<PdfPageAnnotation> CreateAnnotations()
    {
        List<PdfAnnotationBase> rawAnnotations = _pageResources.Annotations ?? new List<PdfAnnotationBase>();
        List<PdfPageAnnotation> annotations = new(rawAnnotations.Count);

        foreach (PdfAnnotationBase annotation in rawAnnotations)
        {
            annotations.Add(new PdfPageAnnotation(this, annotation));
        }

        return annotations;
    }

    /// <summary>
    /// Reads the /Contents entry into the stream sources it names, in the order given.
    /// </summary>
    private static List<PdfObjectStream> ExtractContentStreams(PdfObject pageObject)
    {
        List<PdfObject>? contents = pageObject.Dictionary.GetObjects(PdfTokens.ContentsKey);

        if (contents == null)
        {
            return new List<PdfObjectStream>();
        }

        List<PdfObjectStream> contentStreams = new(contents.Count);

        foreach (PdfObject contentObject in contents)
        {
            contentStreams.Add(contentObject.Stream);
        }

        return contentStreams;
    }
}
