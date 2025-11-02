using PdfReader.Rendering;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfReader.Models
{
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
        /// Render the page content to a Skia canvas.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        public void Draw(SKCanvas canvas)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }
            if (Document == null)
            {
                throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
            }

            canvas.Save();
            var renderer = new PdfContentStreamRenderer(this);
            renderer.ApplyPageTransformations(canvas);
            renderer.RenderContent(canvas);
            canvas.Restore();

            // Release transient per-page cached resources after drawing to reduce memory footprint.
            // Fonts and color spaces retained at document level; this clears name-based page dictionaries.
            if (_lazyPageCache.IsValueCreated)
            {
                Cache.ReleaseCache();
            }
        }
    }
}