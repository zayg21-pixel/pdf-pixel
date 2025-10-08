using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Extracts pages from the PDF document and resolves inherited attributes
    /// (Resources, MediaBox, CropBox, Rotate) prior to constructing <see cref="PdfPage"/> instances.
    /// Instance based to enable structured logging.
    /// </summary>
    public class PdfPageExtractor
    {
        private static readonly SKRect DefaultMediaBox = new SKRect(0, 0, 612, 792);

        private readonly PdfDocument _document;
        private readonly ILogger<PdfPageExtractor> _logger;

        /// <summary>
        /// Create a new page extractor bound to a document.
        /// </summary>
        /// <param name="document">Target PDF document.</param>
        public PdfPageExtractor(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = document.LoggerFactory.CreateLogger<PdfPageExtractor>();
        }

        /// <summary>
        /// Extract all pages in the document populating <see cref="PdfDocument.Pages"/> and <see cref="PdfDocument.PageCount"/>.
        /// RootRef is expected to be established earlier (xref parsing). This method will only set RootRef
        /// if it is currently unset (0) and a /Catalog is discovered during fallback scan.
        /// </summary>
        public void ExtractPages()
        {
            // Primary path: follow stored RootRef if available.
            if (_document.RootRef.IsValid && _document.Objects.TryGetValue(_document.RootRef, out var rootObj))
            {
                var pagesObject = rootObj.Dictionary.GetPageObject(PdfTokens.PagesKey);
                if (pagesObject != null)
                {
                    ExtractPagesFromPagesObject(pagesObject, 1);
                    return;
                }
                _logger.LogWarning("Root object (ref {RootRef}) present but /Pages tree not found.", _document.RootRef);
            }
            else
            {
                _logger.LogWarning("RootRef {RootRef} not found in objects.", _document.RootRef);
            }

            // Last resort: enumerate loose page objects.
            var pageObjects = _document.Objects.Values.Where(o => o.Dictionary.GetName(PdfTokens.TypeKey) == PdfTokens.PageKey).ToList();
            for (int i = 0; i < pageObjects.Count; i++)
            {
                var pageObj = pageObjects[i];
                var page = CreatePageFromObject(pageObj, i + 1);
                _document.Pages.Add(page);
            }
            _document.PageCount = _document.Pages.Count;

            if (_document.PageCount == 0)
            {
                _logger.LogWarning("No page objects found during fallback scan.");
            }
        }

        /// <summary>
        /// Recursively extract pages from a /Pages node, handling nested page trees.
        /// </summary>
        /// <param name="pagesObj">/Pages object node.</param>
        /// <param name="currentPageNum">Current running page number.</param>
        private int ExtractPagesFromPagesObject(PdfObject pagesObj, int currentPageNum)
        {
            var declaredCount = pagesObj.Dictionary.GetIntegerOrDefault(PdfTokens.CountKey);
            if (declaredCount > 0)
            {
                _document.PageCount = declaredCount; // optimistic set
            }

            var kidsArray = pagesObj.Dictionary.GetValue(PdfTokens.KidsKey).AsArray();
            if (kidsArray == null)
            {
                _logger.LogWarning("/Pages node (ref {Ref}) missing /Kids array.", pagesObj.Reference.ObjectNumber);
                return currentPageNum;
            }

            for (int i = 0; i < kidsArray.Count; i++)
            {
                var kidObject = kidsArray.GetPageObject(i);
                if (kidObject == null)
                {
                    _logger.LogWarning("Null kid reference at index {Index} in /Kids array of /Pages ref {Ref}.", i, pagesObj.Reference.ObjectNumber);
                    continue;
                }

                var typeName = kidObject.Dictionary.GetName(PdfTokens.TypeKey);
                if (typeName == PdfTokens.PageKey)
                {
                    var page = CreatePageFromObject(kidObject, currentPageNum++);
                    _document.Pages.Add(page);
                }
                else if (typeName == PdfTokens.PagesKey)
                {
                    currentPageNum = ExtractPagesFromPagesObject(kidObject, currentPageNum);
                }
                else
                {
                    _logger.LogWarning("Unexpected /Type '{Type}' encountered in page tree (ref {Ref}).", typeName, kidObject.Reference.ObjectNumber);
                }
            }

            return currentPageNum;
        }

        /// <summary>
        /// Build a <see cref="PdfPage"/> from a raw /Page object resolving inherited geometry and resources.
        /// </summary>
        private PdfPage CreatePageFromObject(PdfObject pageObj, int pageNumber)
        {
            var resourceDictionary = GetInheritedValue(pageObj, PdfTokens.ResourcesKey).AsDictionary() ?? new PdfDictionary(_document);

            var mediaBox = TryConvertArrayToSKRect(GetInheritedValue(pageObj, PdfTokens.MediaBoxKey).AsArray()) ?? DefaultMediaBox;
            if (mediaBox.Width <= 0 || mediaBox.Height <= 0)
            {
                _logger.LogWarning("Invalid MediaBox on page {PageNum} replaced with default.", pageNumber);
                mediaBox = DefaultMediaBox;
            }

            var cropBox = TryConvertArrayToSKRect(GetInheritedValue(pageObj, PdfTokens.CropBoxKey).AsArray()) ?? mediaBox;
            if (cropBox.Width <= 0 || cropBox.Height <= 0)
            {
                _logger.LogWarning("Invalid CropBox on page {PageNum} replaced with MediaBox.", pageNumber);
                cropBox = mediaBox;
            }

            var rotation = GetNormalizedRotation(GetInheritedValue(pageObj, PdfTokens.RotateKey).AsInteger());

            return new PdfPage(pageNumber, _document, pageObj, mediaBox, cropBox, rotation, resourceDictionary);
        }

        /// <summary>
        /// Convert a PDF rectangle array to <see cref="SKRect"/>.
        /// </summary>
        private SKRect? TryConvertArrayToSKRect(PdfArray value)
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

        /// <summary>
        /// Normalize rotation to 0 / 90 / 180 / 270.
        /// </summary>
        private int GetNormalizedRotation(int rotation)
        {
            return (rotation % 360 + 360) % 360;
        }

        /// <summary>
        /// Walk up the page tree resolving an inherited value.
        /// </summary>
        private IPdfValue GetInheritedValue(PdfObject pageObj, string key)
        {
            var currentObj = pageObj;
            var visited = new HashSet<int>();
            while (currentObj != null && !visited.Contains(currentObj.Reference.ObjectNumber))
            {
                visited.Add(currentObj.Reference.ObjectNumber);
                if (currentObj.Dictionary.HasKey(key))
                {
                    return currentObj.Dictionary.GetValue(key);
                }
                currentObj = currentObj.Dictionary.GetPageObject(PdfTokens.ParentKey);
            }
            return null;
        }
    }
}