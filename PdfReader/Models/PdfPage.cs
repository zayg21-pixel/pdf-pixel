using PdfReader.Fonts;
using PdfReader.Fonts.Types;
using PdfReader.Rendering;
using SkiaSharp;
using System;

namespace PdfReader.Models
{
    /// <summary>
    /// Represents a single PDF page with its resolved geometry, resources, and underlying /Page object.
    /// All geometry (MediaBox, CropBox, Rotation) and the resource dictionary are resolved beforehand
    /// by <see cref="Parsing.PdfPageExtractor"/>. This class is a pure data model with minimal logic.
    /// </summary>
    public class PdfPage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfPage"/> class with fully resolved attributes.
        /// Caller must guarantee that <paramref name="mediaBox"/> and <paramref name="cropBox"/> are valid rectangles
        /// and that <paramref name="resourceDictionary"/> is non-null.
        /// </summary>
        /// <param name="pageNumber">1-based page index within the document.</param>
        /// <param name="document">Owning <see cref="PdfDocument"/>.</param>
        /// <param name="pageObject">Underlying /Page dictionary object.</param>
        /// <param name="mediaBox">Resolved MediaBox rectangle.</param>
        /// <param name="cropBox">Resolved CropBox rectangle.</param>
        /// <param name="rotation">Normalized rotation in degrees (0, 90, 180, 270).</param>
        /// <param name="resourceDictionary">Resolved resource dictionary (must not be null).</param>
        public PdfPage(int pageNumber,
                       PdfDocument document,
                       PdfObject pageObject,
                       SKRect mediaBox,
                       SKRect cropBox,
                       int rotation,
                       PdfDictionary resourceDictionary)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (pageObject == null)
            {
                throw new ArgumentNullException(nameof(pageObject));
            }
            if (resourceDictionary == null)
            {
                throw new ArgumentNullException(nameof(resourceDictionary));
            }

            PageNumber = pageNumber;
            Document = document;
            PageObject = pageObject;
            MediaBox = mediaBox;
            CropBox = cropBox;
            Rotation = rotation;
            ResourceDictionary = resourceDictionary;
        }

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
        /// Retrieve a font by its resource key from the page resources (with document-level caching).
        /// Returns null if the font reference does not exist or cannot be created.
        /// </summary>
        /// <param name="fontName">Resource key of the font.</param>
        /// <returns>Resolved <see cref="PdfFontBase"/> or null.</returns>
        public virtual PdfFontBase GetFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return null;
            }

            var fontDict = ResourceDictionary.GetDictionary(PdfTokens.FontKey);
            if (fontDict == null)
            {
                return null;
            }

            var fontObject = fontDict.GetPageObject(fontName);

            var fontReference = fontObject.Reference;
            if (fontReference.IsValid && Document.Fonts.TryGetValue(fontReference, out var cachedFont))
            {
                return cachedFont;
            }

            var newFont = PdfFontFactory.CreateFont(fontObject);

            if (newFont != null && fontReference.IsValid)
            {
                Document.Fonts[fontReference] = newFont;
            }

            return newFont;
        }

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
        }
    }
}