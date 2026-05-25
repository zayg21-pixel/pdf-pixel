using PdfPixel.Rendering;
using PdfPixel.TextExtraction;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
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
    private static readonly SKRect DefaultMediaBox = new SKRect(0, 0, 612, 792);

    private readonly Lazy<PdfPageCache> _lazyPageCache;
    private readonly IPdfDocumentInternal _document;
    private readonly PdfObject _pageObject;
    private readonly PdfPageResources _pageResources;
    private readonly PdfDictionary _resourceDictionary;

    /// <summary>
    /// Initializes a new instance using <see cref="PdfPageResources"/> snapshot (rotation already normalized there).
    /// </summary>
    /// <param name="pageNumber">1-based page index.</param>
    /// <param name="pageLabel">Resolved page label for this page.</param>
    /// <param name="document">Owning document.</param>
    /// <param name="pageObject">Underlying /Page object.</param>
    /// <param name="pageResources">Resolved inheritable page resources snapshot.</param>
    internal PdfPage(int pageNumber,
                   PdfString pageLabel,
                   IPdfDocumentInternal document,
                   PdfObject pageObject,
                   PdfPageResources pageResources)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _pageObject = pageObject ?? throw new ArgumentNullException(nameof(pageObject));
        _pageResources = pageResources ?? throw new ArgumentNullException(nameof(pageResources));
        _lazyPageCache = new Lazy<PdfPageCache>(() => new PdfPageCache(this));

        PageNumber = pageNumber;
        var media = pageResources.MediaBoxRect ?? DefaultMediaBox;
        var crop = pageResources.CropBoxRect ?? media;
        MediaBox = media;
        CropBox = crop;
        Rotation = pageResources.Rotate ?? 0;
        _resourceDictionary = pageResources.Resources ?? new PdfDictionary(document);
        Annotations = pageResources.Annotations ?? [];
        PageLabel = pageLabel;
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
    public IReadOnlyList<PdfAnnotationBase> Annotations { get; }

    /// <inheritdoc/>
    public PdfString PageLabel { get; }

    PdfPageCache IPdfPageInternal.Cache => _lazyPageCache.Value;

    PdfPageResources IPdfPageInternal.PageResources => _pageResources;

    PdfObject IPdfPageInternal.PageObject => _pageObject;

    PdfDictionary IPdfPageInternal.ResourceDictionary => _resourceDictionary;

    IPdfDocumentInternal IPdfPageInternal.Document => _document;

    /// <inheritdoc/>
    public void Draw(IPdfCommandProcessor processor, IPdfExecutionObserver observer)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }
        if (_document == null)
        {
            throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
        }

        var renderer = new PdfRenderer(_document.LoggerFactory);
        var contentRenderer = new PdfContentStreamRenderer(renderer, this);

        contentRenderer.RenderContent(processor, observer);
    }

    /// <inheritdoc/>
    public void RenderAnnotations(
        IPdfCommandProcessor processor,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationBase activeAnnotation,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfExecutionObserver observer)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (_document == null)
        {
            throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
        }

        var renderer = new PdfRenderer(_document.LoggerFactory);
        var annotationRenderer = new PdfAnnotationRenderer(renderer, this);
        annotationRenderer.RenderAnnotations(processor, renderingParameters, activeAnnotation, visualStateKind, observer);
    }

    /// <inheritdoc/>
    public List<PdfCharacter> ExtractText()
    {
        // TODO: text extraction should be done from commands
        return new List<PdfCharacter>();
    }
}
