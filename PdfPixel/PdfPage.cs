using PdfPixel.Rendering;
using PdfPixel.Annotations;
using PdfPixel.Files;
using PdfPixel.Navigation;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using PdfPixel.Geometry;
using PdfPixel.Actions;
using PdfPixel.Commands;
using PdfPixel.Imaging;
using PdfPixel.Measures;
using PdfPixel.Objects;
using PdfPixel.Resources;
using PdfPixel.Transparency;

namespace PdfPixel;

/// <summary>
/// Represents a single PDF page with its resolved geometry, resources, and underlying /Page object.
/// All geometry (MediaBox, CropBox, Rotation) and the resource dictionary are resolved beforehand
/// by <see cref="Parsing.PdfPageExtractor"/>. This class is a pure data model with minimal logic.
/// </summary>
internal class PdfPage : IPdfPageInternal
{
    private static readonly PdfRectangle DefaultMediaBox = new(0, 0, 612, 792);

    private readonly IPdfDocumentInternal _document;
    private readonly PdfDictionary _pageDictionary;
    private readonly PdfReference? _pageReference;
    private readonly List<PdfObjectStream> _contentStreams;
    private readonly PdfPageResources _pageResources;
    private readonly PdfResourceNode _resources;
    private readonly PdfTransparencyGroup? _transparencyGroup;
    private PdfPageAnnotation[]? _annotations;

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

        PdfDictionary pageDictionary = pageObject.Dictionary;
        MediaBox = media;
        CropBox = crop;
        BleedBox = ResolveBoundary(PdfRectangle.FromArray(pageDictionary.GetArray(PdfTokens.BleedBoxKey)), media, crop);
        TrimBox = ResolveBoundary(PdfRectangle.FromArray(pageDictionary.GetArray(PdfTokens.TrimBoxKey)), media, crop);
        ArtBox = ResolveBoundary(PdfRectangle.FromArray(pageDictionary.GetArray(PdfTokens.ArtBoxKey)), media, crop);
        Rotation = pageResources.Rotate ?? 0;
        _resources = pageResources.Resources ?? PdfResourceNode.CreateEmpty(document);
        PageLabel = pageLabel;
        _pageDictionary = pageDictionary;

        _transparencyGroup = PdfTransparencyGroup.GetTransparencyGroup(pageObject.Dictionary, PdfTokens.GroupKey);
        Thumbnail = PdfImage.GetImage(pageDictionary, PdfTokens.ThumbnailKey);
        Beads = PdfBead.FromPage(pageDictionary);
        Viewports = PdfViewport.FromPage(pageDictionary);

        UserUnit = pageDictionary.GetFloat(PdfTokens.UserUnitKey) ?? 1f;
        StructParents = pageDictionary.GetInteger(PdfTokens.StructParentsKey);
        LastModified = PdfDateParser.ParsePdfDate(pageDictionary.GetString(PdfTokens.LastModifiedKey));
        Metadata = PdfMetadata.FromDictionary(pageDictionary);
        AssociatedFiles = PdfFileSpecification.GetFileSpecifications(pageDictionary.GetArray(PdfTokens.AssociatedFilesKey));
        Duration = pageDictionary.GetFloat(PdfTokens.DurKey);
        Tabs = pageDictionary.GetName(PdfTokens.TabsKey)?.AsEnum<PdfTabOrder>();
        TemplateInstantiated = pageDictionary.GetName(PdfTokens.TemplateInstantiatedKey);
        Id = pageDictionary.GetString(PdfTokens.WebCaptureIdKey);
        PreferredZoom = pageDictionary.GetFloat(PdfTokens.PZKey);
        AdditionalActions = PdfPageAdditionalActions.FromPage(pageDictionary);
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
    public PdfString PageLabel { get; }

    /// <inheritdoc/>
    public PdfImage? Thumbnail { get; }

    /// <inheritdoc/>
    public PdfRectangle BleedBox { get; }

    /// <inheritdoc/>
    public PdfRectangle TrimBox { get; }

    /// <inheritdoc/>
    public PdfRectangle ArtBox { get; }

    /// <inheritdoc/>
    public float UserUnit { get; }

    /// <inheritdoc/>
    public int? StructParents { get; }

    /// <inheritdoc/>
    public DateTime? LastModified { get; }

    /// <inheritdoc/>
    public PdfMetadata? Metadata { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <inheritdoc/>
    public float? Duration { get; }

    /// <inheritdoc/>
    public PdfTabOrder? Tabs { get; }

    /// <inheritdoc/>
    public PdfString? TemplateInstantiated { get; }

    /// <inheritdoc/>
    public PdfString? Id { get; }

    /// <inheritdoc/>
    public float? PreferredZoom { get; }

    /// <inheritdoc/>
    public PdfPageAdditionalActions? AdditionalActions { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PdfBead> Beads { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PdfViewport> Viewports { get; }

    PdfResourceNode IPdfContentHost.Resources => _resources;

    PdfPageResources IPdfPageInternal.PageResources => _pageResources;

    PdfReference? IPdfPageInternal.PageReference => _pageReference;

    int? IPdfContentHost.StructParents => StructParents;

    IReadOnlyList<PdfObjectStream> IPdfPageInternal.ContentStreams => _contentStreams;

    IPdfDocumentInternal IPdfContentHost.Document => _document;

    PdfTransparencyGroup? IPdfPageInternal.TransparencyGroup => _transparencyGroup;

    /// <inheritdoc/>
    public IReadOnlyList<PdfPageAnnotation> GetAnnotations()
    {
        if (_annotations == null)
        {
            _annotations = CreateAnnotations();
        }

        return _annotations;
    }

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
    /// Parses the page's /Annots entries and binds each annotation to the page itself.
    /// </summary>
    private PdfPageAnnotation[] CreateAnnotations()
    {
        PdfArray? annotationArray = _pageDictionary.GetArray(PdfTokens.AnnotsKey);

        if (annotationArray == null)
        {
            return Array.Empty<PdfPageAnnotation>();
        }

        List<PdfPageAnnotation> annotations = new(annotationArray.Count);

        for (int index = 0; index < annotationArray.Count; index++)
        {
            PdfAnnotation? annotation = PdfAnnotation.GetAnnotation(annotationArray, index);
            if (annotation != null)
            {
                annotations.Add(new PdfPageAnnotation(this, annotation));
            }
        }

        return annotations.ToArray();
    }

    /// <summary>
    /// Resolves a page boundary to its intersection with the MediaBox, falling back to the CropBox
    /// when undeclared or when the intersection is empty (ISO 32000-2, 14.11.2).
    /// </summary>
    private static PdfRectangle ResolveBoundary(PdfRectangle? declared, in PdfRectangle media, in PdfRectangle crop)
    {
        if (declared == null)
        {
            return crop;
        }

        PdfRectangle visible = PdfRectangle.Intersect(declared.Value, media);
        if (visible.Width > 0 && visible.Height > 0)
        {
            return visible;
        }

        return crop;
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
