using PdfPixel.Rendering;
using PdfPixel.Annotations.Model;
using PdfPixel.Imaging.Model;
using PdfPixel.Streams;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using System;
using System.Collections.Generic;
using PdfPixel.Geometry;
using PdfPixel.Commands.Model;

namespace PdfPixel.Models;

/// <summary>
/// Represents a single PDF page with its resolved geometry, resources, and underlying /Page object.
/// All geometry (MediaBox, CropBox, Rotation) and the resource dictionary are resolved beforehand
/// by <see cref="Parsing.PdfPageExtractor"/>. This class is a pure data model with minimal logic.
/// </summary>
internal class PdfPage : IPdfPageInternal
{
    private static readonly PdfRectangle DefaultMediaBox = new(0, 0, 612, 792);

    private readonly Lazy<PdfContentHostCache> _pageCache;
    private readonly Lazy<IReadOnlyList<PdfPageAnnotation>> _annotations;
    private readonly IPdfDocumentInternal _document;
    private readonly PdfReference _pageReference;
    private readonly List<PdfObjectStream> _contentStreams;
    private readonly PdfPageResources _pageResources;
    private readonly PdfDictionary _resourceDictionary;
    private readonly PdfTransparencyGroup? _transparencyGroup;
    private readonly int? _structParents;
    private readonly Lazy<PdfImage?> _thumbnail;

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
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (pageObject == null)
        {
            throw new ArgumentNullException(nameof(pageObject));
        }

        _pageResources = pageResources ?? throw new ArgumentNullException(nameof(pageResources));
        _pageReference = pageObject.Reference;
        _contentStreams = ExtractContentStreams(pageObject);
        PdfDictionary resourceDictionary = pageResources.Resources ?? new PdfDictionary(document);

        PageNumber = pageNumber;
        PdfRectangle media = DefaultMediaBox;
        PdfRectangle? declaredMedia = pageResources.MediaBoxRect;
        if (declaredMedia != null && declaredMedia.Value.Width > 0 && declaredMedia.Value.Height > 0)
        {
            media = declaredMedia.Value;
        }

        PdfRectangle crop = media;
        PdfRectangle? declaredCrop = pageResources.CropBoxRect;
        if (declaredCrop != null)
        {
            PdfRectangle visibleCrop = PdfRectangle.Intersect(declaredCrop.Value, media);
            if (visibleCrop.Width > 0 && visibleCrop.Height > 0)
            {
                crop = visibleCrop;
            }
        }

        MediaBox = media;
        CropBox = crop;
        Rotation = pageResources.Rotate ?? 0;
        _resourceDictionary = resourceDictionary;
        PageLabel = pageLabel;
        _pageCache = new Lazy<PdfContentHostCache>(() => new PdfContentHostCache(this, document, resourceDictionary));

        _annotations = new Lazy<IReadOnlyList<PdfPageAnnotation>>(CreateAnnotations);

        _transparencyGroup = PdfSoftMaskParser.ParseTransparencyGroup(pageObject.Dictionary, PdfTokens.GroupKey, this);
        _structParents = pageObject.Dictionary.GetInteger(PdfTokens.StructParentsKey);
        _thumbnail = new Lazy<PdfImage?>(() => PdfImage.FromDictionaryEntry(pageObject.Dictionary, PdfTokens.ThumbnailKey));
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

    /// <inheritdoc/>
    public PdfImage? Thumbnail => _thumbnail.Value;

    PdfContentHostCache IPdfContentHost.Cache => _pageCache.Value;

    PdfPageResources IPdfPageInternal.PageResources => _pageResources;

    PdfReference IPdfPageInternal.PageReference => _pageReference;

    int? IPdfContentHost.StructParents => _structParents;

    IReadOnlyList<PdfObjectStream> IPdfPageInternal.ContentStreams => _contentStreams;

    PdfDictionary IPdfContentHost.ResourceDictionary => _resourceDictionary;

    IPdfDocumentInternal IPdfContentHost.Document => _document;

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

        bool needsGroupLayer = _transparencyGroup != null && _transparencyGroup.RequiresLayer;

        if (needsGroupLayer)
        {
            processor.Process(new SaveLayerCommand(CropBox));
        }

        contentRenderer.RenderContent(processor, _contentStreams, MediaBox, renderingParameters, observer);

        if (needsGroupLayer)
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
