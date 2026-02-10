using PdfPixel.Rendering;
using PdfPixel.TextExtraction;
using PdfPixel.Annotations.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.Models;

/// <summary>
/// Represents a single PDF page with its resolved geometry, resources, and underlying /Page object.
/// All geometry (MediaBox, CropBox, Rotation) and the resource dictionary are resolved beforehand
/// by <see cref="Parsing.PdfPageExtractor"/>. This class is a pure data model with minimal logic.
/// </summary>
public class PdfPage
{
    private static readonly SKRect DefaultMediaBox = new SKRect(0, 0, 612, 792);

    // Lazy per-page cache (thread-safe). Allocated only on first access.
    private readonly Lazy<PdfPageCache> _lazyPageCache;

    /// <summary>
    /// Initializes a new instance using <see cref="PdfPageResources"/> snapshot (rotation already normalized there).
    /// </summary>
    /// <param name="pageNumber">1-based page index.</param>
    /// <param name="document">Owning document.</param>
    /// <param name="pageObject">Underlying /Page object.</param>
    /// <param name="pageResources">Resolved inheritable page resources snapshot.</param>
    public PdfPage(int pageNumber,
                   PdfDocument document,
                   PdfObject pageObject,
                   PdfPageResources pageResources)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        PageObject = pageObject ?? throw new ArgumentNullException(nameof(pageObject));
        PageResources = pageResources ?? throw new ArgumentNullException(nameof(pageResources));
        _lazyPageCache = new Lazy<PdfPageCache>(() => new PdfPageCache(this), LazyThreadSafetyMode.ExecutionAndPublication);

        PageNumber = pageNumber;
        var media = pageResources.MediaBoxRect ?? DefaultMediaBox;
        var crop = pageResources.CropBoxRect ?? media;
        MediaBox = media;
        CropBox = crop;
        Rotation = pageResources.Rotate ?? 0;
        ResourceDictionary = pageResources.Resources ?? new PdfDictionary(document);
        Annotations = pageResources.Annotations ?? [];
    }

    /// <summary>
    /// Lazy per-page resource cache providing name-based lookups. Internal access only.
    /// Created on first access to avoid unnecessary allocations for pages that do not need caching.
    /// </summary>
    internal virtual PdfPageCache Cache => _lazyPageCache.Value;

    /// <summary>
    /// Page resources snapshot used to resolve inheritable attributes.
    /// </summary>
    internal PdfPageResources PageResources { get; }

    /// <summary>
    /// 1-based index of this page within the document.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Underlying /Page object supplying dictionary entries and content references.
    /// </summary>
    public virtual PdfObject PageObject { get; }

    /// <summary>
    /// Resolved resource dictionary for this page (never null).
    /// </summary>
    public virtual PdfDictionary ResourceDictionary { get; }

    /// <summary>
    /// Resolved MediaBox rectangle.
    /// </summary>
    public SKRect MediaBox { get; }

    /// <summary>
    /// Resolved CropBox rectangle.
    /// </summary>
    public SKRect CropBox { get; }

    /// <summary>
    /// Normalized page rotation in degrees (0, 90, 180, 270).
    /// </summary>
    public int Rotation { get; }

    /// <summary>
    /// Owning document instance.
    /// </summary>
    public PdfDocument Document { get; }

    /// <summary>
    /// Gets the annotations for this page.
    /// Resolved during page construction from the /Annots array and inheritable annotations.
    /// </summary>
    public IReadOnlyList<PdfAnnotationBase> Annotations { get; }

    /// <summary>
    /// Render the page content to a Skia canvas.
    /// </summary>
    /// <param name="canvas">Destination canvas.</param>
    /// <param name="renderingParameters">Rendering parameters for rendering in defined canvas.</param>
    public void Draw(SKCanvas canvas, PdfRenderingParameters renderingParameters)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }
        if (Document == null)
        {
            throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
        }

        var renderer = new PdfRenderer(Document.LoggerFactory);
        var contentRenderer = new PdfContentStreamRenderer(renderer, this);

        contentRenderer.RenderContent(canvas, renderingParameters);
    }

    /// <summary>
    /// Render annotations for this page on the provided canvas with an optional active annotation and visual state.
    /// </summary>
    /// <param name="canvas">Destination canvas.</param>
    /// <param name="renderingParameters">Rendering parameters for rendering in defined canvas.</param>
    /// <param name="activeAnnotation">Annotation that should be rendered in a non-normal visual state, or null.</param>
    /// <param name="visualStateKind">Visual state to apply to the active annotation.</param>
    public void RenderAnnotations(
        SKCanvas canvas,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationBase activeAnnotation,
        PdfAnnotationVisualStateKind visualStateKind)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        if (Document == null)
        {
            throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
        }

        var renderer = new PdfRenderer(Document.LoggerFactory);
        var annotationRenderer = new PdfAnnotationRenderer(renderer, this);
        annotationRenderer.RenderAnnotations(canvas, renderingParameters, activeAnnotation, visualStateKind);
    }

    /// <summary>
    /// Extract text content from the page.
    /// </summary>
    /// <returns></returns>
    public List<PdfCharacter> ExtractText()
    {
        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(new SKRect(0, 0, 1, 1));

        var textExtractor = new PdfTextExtractionRenderer();
        
        var contentRenderer = new PdfContentStreamRenderer(textExtractor, this);
        contentRenderer.RenderContent(canvas, new PdfRenderingParameters());

        return textExtractor.PageCharacters;
    }
}