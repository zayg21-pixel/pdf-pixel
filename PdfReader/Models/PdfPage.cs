using Microsoft.Extensions.Logging;
using PdfReader.Fonts;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Streams;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Models
{
    public class PdfPage
    {
        private static readonly SKRect DefaultSize = new SKRect(0, 0, 612, 792);

        public PdfPage(int pageNumber, PdfDocument document, PdfObject pageObject)
        {
            PageNumber = pageNumber;
            Document = document;
            PageObject = pageObject;
            var resourceDictionary = PdfPageExtractor.GetInheritedValue(PageObject, PdfTokens.ResourcesKey).AsDictionary();
            MediaBox = PdfPageExtractor.TryConvertArrayToSKRect(PdfPageExtractor.GetInheritedValue(pageObject, PdfTokens.MediaBoxKey).AsArray()) ?? DefaultSize;
            CropBox = PdfPageExtractor.TryConvertArrayToSKRect(PdfPageExtractor.GetInheritedValue(pageObject, PdfTokens.CropBoxKey).AsArray()) ?? MediaBox;
            Rotation = PdfPageExtractor.GetNormalizedRotation(PdfPageExtractor.GetInheritedValue(pageObject, PdfTokens.CropBoxKey).AsInteger());

            ResourceDictionary = resourceDictionary;
        }

        public int PageNumber { get; }
        public virtual PdfObject PageObject { get; }
        public virtual PdfDictionary ResourceDictionary { get; }
        public SKRect MediaBox { get; }
        public SKRect CropBox { get; }
        public int Rotation { get; }
        public PdfDocument Document { get; }

        /// <summary>
        /// Get a font by name from this page's resources (with inheritance support)
        /// Updated to return PdfFontBase hierarchy
        /// </summary>
        public virtual PdfFontBase GetFont(string fontName)
        {
            var fontDict = ResourceDictionary.GetDictionary(PdfTokens.FontKey);

            if (fontDict == null) return null;
            
            // Resolve the font object (normalized keys handled by PdfDictionary)
            var fontObject = fontDict.GetPageObject(fontName);

            if (fontObject != null)
            {
                var fontReference = fontObject.Reference;
                
                // Check if font is already cached in document
                if (Document.Fonts.TryGetValue(fontReference, out var cachedFont))
                {
                    return cachedFont;
                }
                
                // Load font using factory and cache it
                var newFont = PdfFontFactory.CreateFont(fontObject);
                if (newFont != null)
                {
                    Document.Fonts[fontReference] = newFont;
                    return newFont;
                }
            }

            return null;
        }
        
        /// <summary>
        /// Renders the PDF page content directly to the provided SkiaSharp canvas
        /// </summary>
        /// <param name="canvas">The canvas to draw on</param>
        public void Draw(SKCanvas canvas)
        {
            if (Document == null)
                throw new InvalidOperationException("Document reference not set. This page was not properly loaded from a document.");
                
            // Set up canvas for PDF coordinate system (origin bottom-left)
            canvas.Save();

            var renderer = new PdfContentStreamRenderer(this);

            // Apply page-level transformations in correct order
            renderer.ApplyPageTransformations(canvas);

            renderer.RenderContent(canvas);

            canvas.Restore();
        }
    }
}