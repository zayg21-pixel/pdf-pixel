using PdfReader.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles extraction of pages from PDF document structure
    /// </summary>
    public static class PdfPageExtractor
    {
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
            
            for (int i = 0; i < pageObjects.Count; i++)
            {
                var pageObj = pageObjects[i];
                var page = CreatePageFromObject(document, pageObj, i + 1);
                document.Pages.Add(page);
            }
            
            document.PageCount = document.Pages.Count;
        }

        /// <summary>
        /// Extract pages from a pages object (handles page tree hierarchy)
        /// </summary>
        public static int ExtractPagesFromPagesObject(PdfDocument document, PdfObject pagesObj, int currentPageNum)
        {
            var count = pagesObj.Dictionary.GetInteger(PdfTokens.CountKey);
            if (count > 0)
            {
                document.PageCount = count;
            }
            
            var kidsArray = pagesObj.Dictionary.GetValue(PdfTokens.KidsKey).AsReferenceArray();
            if (kidsArray != null)
            {
                foreach (var kidValue in kidsArray)
                {
                    if (!document.Objects.TryGetValue(kidValue.ObjectNumber, out var kidObject))
                    {
                        continue;
                    }

                    if (kidObject.Dictionary.GetName(PdfTokens.TypeKey) == PdfTokens.PageKey)
                    {
                        var page = CreatePageFromObject(document, kidObject, currentPageNum++);
                        document.Pages.Add(page);
                    }
                    else if (kidObject.Dictionary.GetName(PdfTokens.TypeKey) == PdfTokens.PagesKey)
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
            var page = new PdfPage(pageNumber, document, pageObj);
            
            return page;
        }

        /// <summary>
        /// Utility method to convert a PDF array value to SKRect
        /// </summary>
        /// <param name="value">The PDF value to convert</param>
        /// <returns>SKRect if conversion successful, null otherwise</returns>
        public static SKRect? TryConvertArrayToSKRect(List<IPdfValue> value)
        {
            if (value == null)
                return null;

            if (value.Count < 4)
                return null;

            var left = value[0].AsFloat();
            var top = value[1].AsFloat();
            var right = value[2].AsFloat();
            var bottom = value[3].AsFloat();

            return new SKRect(left, top, right, bottom);
        }

        public static int GetNormalizedRotation(int rotation)
        {
            // Normalize rotation to 0, 90, 180, or 270 degrees
            return (rotation % 360 + 360) % 360;
        }

        public static IPdfValue GetInheritedValue(PdfDocument document, PdfObject pageObj, string key)
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