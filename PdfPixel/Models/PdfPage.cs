using PdfPixel.Rendering;
using PdfPixel.TextExtraction;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a single PDF page with its resolved geometry, resources, and underlying /Page object.
/// All geometry (MediaBox, CropBox, Rotation) and the resource dictionary are resolved beforehand
/// by <see cref="Parsing.PdfPageExtractor"/>. This class is a pure data model with minimal logic.
/// </summary>
internal class PdfPage : IPdfPageInternal
{
    private static readonly SKRect DefaultMediaBox = new(0, 0, 612, 792);

    private readonly PdfPageCache _pageCache;
    private readonly IPdfDocumentInternal _document;
    private readonly PdfObject _pageObject;
    private readonly PdfPageResources _pageResources;
    private readonly PdfDictionary _resourceDictionary;
    private readonly PdfTransparencyGroup? _transparencyGroup;

    protected internal PdfPage(
        int pageNumber,
        in PdfString pageLabel,
        IPdfDocumentInternal document,
        PdfObject pageObject,
        PdfPageResources pageResources,
        PdfDictionary resourceDictionary)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _pageObject = pageObject ?? throw new ArgumentNullException(nameof(pageObject));
        _pageResources = pageResources ?? throw new ArgumentNullException(nameof(pageResources));

        PageNumber = pageNumber;
        SKRect media = pageResources.MediaBoxRect ?? DefaultMediaBox;
        SKRect crop = pageResources.CropBoxRect ?? media;
        MediaBox = media;
        CropBox = crop;
        Rotation = pageResources.Rotate ?? 0;
        _resourceDictionary = resourceDictionary;
        PageLabel = pageLabel;
        _pageCache = new PdfPageCache(this, document, resourceDictionary);

        List<PdfAnnotationBase> rawAnnotations = pageResources.Annotations ?? new List<PdfAnnotationBase>();
        List<PdfPageAnnotation> annotations = new(rawAnnotations.Count);

        foreach (PdfAnnotationBase annotation in rawAnnotations)
        {
            annotations.Add(new PdfPageAnnotation(this, annotation));
        }

        Annotations = annotations;

        PdfDictionary? groupDict = _pageObject.Dictionary.GetDictionary(PdfTokens.GroupKey);
        _transparencyGroup = PdfSoftMaskParser.ParseTransparencyGroup(groupDict, this);
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
    public SKRect MediaBox { get; }

    /// <inheritdoc/>
    public SKRect CropBox { get; }

    /// <inheritdoc/>
    public int Rotation { get; }

    /// <inheritdoc/>
    public IReadOnlyList<PdfPageAnnotation> Annotations { get; }

    /// <inheritdoc/>
    public PdfString PageLabel { get; }

    PdfPageCache IPdfPageInternal.Cache => _pageCache;

    PdfPageResources IPdfPageInternal.PageResources => _pageResources;

    PdfObject IPdfPageInternal.PageObject => _pageObject;

    PdfDictionary IPdfPageInternal.ResourceDictionary => _resourceDictionary;

    IPdfDocumentInternal IPdfPageInternal.Document => _document;

    PdfTransparencyGroup? IPdfPageInternal.TransparencyGroup => _transparencyGroup;

    /// <inheritdoc/>
    public void RenderContent(IPdfCommandProcessor processor, IPdfExecutionObserver observer)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (_document == null)
        {
            throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
        }

        PdfRenderer renderer = new(_document.LoggerFactory);
        PdfContentStreamRenderer contentRenderer = new(renderer, this);

        if (_transparencyGroup != null)
        {
            processor.Process(new SaveLayerCommand(CropBox, paint: null));
        }

        contentRenderer.RenderContent(processor, observer);

        if (_transparencyGroup != null)
        {
            processor.Process(new RestoreStateCommand());
        }
    }

    /// <inheritdoc/>
    public List<PdfCharacter> ExtractText()
    {
        // TODO: text extraction should be done from commands
        return [];
    }
}
