using PdfReader.Models;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles extraction of pages from PDF document structure.
    /// Resolves inherited attributes (Resources, MediaBox, CropBox, Rotate) before constructing PdfPage instances.
    /// </summary>
    public static class PdfPageExtractor
    {
        private static readonly SKRect DefaultMediaBox = new SKRect(0, 0, 612, 792);

        /// <summary>
        /// Extract all pages from the PDF document
        /// </summary>
        public static void ExtractPages(PdfDocument document)
        {
            // First try the traditional approach
            if (document.RootRef > 0 && document.Objects.TryGetValue(document.RootRef, out var rootObj))
            {
                var pagesObject = rootObj.Dictionary.GetPageObject(PdfTokens.PagesKey);
                if (pagesObject != null)
                {
                    ExtractPagesFromPagesObject(document, pagesObject, 1);
                    return;
                }
            }
            
            // Fallback: Find root and pages objects by scanning all objects
            var catalogObj = document.Objects.Values.FirstOrDefault(obj => obj.Dictionary?.GetName(PdfTokens.TypeKey) == PdfTokens.CatalogKey);
            
            if (catalogObj != null)
            {
                document.RootRef = catalogObj.Reference.ObjectNumber;
                
                var pagesObject = catalogObj.Dictionary.GetPageObject(PdfTokens.PagesKey);
                if (pagesObject != null)
                {
                    ExtractPagesFromPagesObject(document, pagesObject, 1);

                    if (document.PageCount != 0)
                    {
                        return;
                    }
                }
            }

            var pageObjects = document.Objects.Values.Where(obj => obj.Dictionary.GetName(PdfTokens.TypeKey) == PdfTokens.PageKey).ToList();
            
            for (int index = 0; index < pageObjects.Count; index++)
            {
                var pageObj = pageObjects[index];
                var page = CreatePageFromObject(document, pageObj, index + 1);
                document.Pages.Add(page);
            }
            
            document.PageCount = document.Pages.Count;
        }

        /// <summary>
        /// Extract pages from a pages object (handles page tree hierarchy)
        /// </summary>
        public static int ExtractPagesFromPagesObject(PdfDocument document, PdfObject pagesObj, int currentPageNum)
        {
            var count = pagesObj.Dictionary.GetIntegerOrDefault(PdfTokens.CountKey);
            if (count > 0)
            {
                document.PageCount = count;
            }
            
            var kidsArray = pagesObj.Dictionary.GetValue(PdfTokens.KidsKey).AsArray();
            if (kidsArray != null)
            {
                for (int i = 0; i < kidsArray.Count; i++)
                {
                    var kidObject = kidsArray.GetPageObject(i);
                    var typeName = kidObject.Dictionary.GetName(PdfTokens.TypeKey);
                    if (typeName == PdfTokens.PageKey)
                    {
                        var page = CreatePageFromObject(document, kidObject, currentPageNum++);
                        document.Pages.Add(page);
                    }
                    else if (typeName == PdfTokens.PagesKey)
                    {
                        currentPageNum = ExtractPagesFromPagesObject(document, kidObject, currentPageNum);
                    }
                }
            }
            
            return currentPageNum;
        }

        /// <summary>
        /// Create a PdfPage from a page object
        /// </summary>
        private static PdfPage CreatePageFromObject(PdfDocument document, PdfObject pageObj, int pageNumber)
        {
            var resourceDictionary = GetInheritedValue(pageObj, PdfTokens.ResourcesKey).AsDictionary() ?? new PdfDictionary(document);

            var mediaBox = TryConvertArrayToSKRect(GetInheritedValue(pageObj, PdfTokens.MediaBoxKey).AsArray()) ?? DefaultMediaBox;
            if (mediaBox.Width <= 0 || mediaBox.Height <= 0)
            {
                mediaBox = DefaultMediaBox;
            }

            var cropBox = TryConvertArrayToSKRect(GetInheritedValue(pageObj, PdfTokens.CropBoxKey).AsArray()) ?? mediaBox;
            if (cropBox.Width <= 0 || cropBox.Height <= 0)
            {
                cropBox = mediaBox;
            }

            var rotation = GetNormalizedRotation(GetInheritedValue(pageObj, PdfTokens.RotateKey).AsInteger());

            return new PdfPage(pageNumber, document, pageObj, mediaBox, cropBox, rotation, resourceDictionary);
        }

        /// <summary>
        /// Utility method to convert a PDF array value to SKRect
        /// </summary>
        /// <param name="value">The PDF value to convert</param>
        /// <returns>SKRect if conversion successful, null otherwise</returns>
        public static SKRect? TryConvertArrayToSKRect(PdfArray value)
        {
            if (value == null)
            {
                return null;
            }
            if (value.Count < 4)
            {
                return null;
            }

            var left = value.GetFloat(0);
            var top = value.GetFloat(1);
            var right = value.GetFloat(2);
            var bottom = value.GetFloat(3);
            return new SKRect(left, top, right, bottom);
        }

        public static int GetNormalizedRotation(int rotation)
        {
            // Normalize rotation to 0, 90, 180, or 270 degrees
            return (rotation % 360 + 360) % 360;
        }

        public static IPdfValue GetInheritedValue(PdfObject pageObj, string key)
        {
            var currentObj = pageObj;
            var checkedObjects = new HashSet<int>();
            while (currentObj != null && !checkedObjects.Contains(currentObj.Reference.ObjectNumber))
            {
                checkedObjects.Add(currentObj.Reference.ObjectNumber);
                // Check for the value at this level
                if (currentObj.Dictionary.HasKey(key))
                {
                    return currentObj.Dictionary.GetValue(key);
                }
                // Move to parent page object
                currentObj = currentObj.Dictionary.GetPageObject(PdfTokens.ParentKey);
            }
            return null; // Not found in hierarchy
        }
    }
}