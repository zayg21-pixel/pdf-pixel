using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Extracts pages from the PDF document and resolves inherited attributes
/// (Resources, MediaBox, CropBox, Rotate) prior to constructing <see cref="PdfPage"/> instances.
/// Instance based to enable structured logging.
/// </summary>
internal class PdfPageExtractor
{
    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfPageExtractor> _logger;

    /// <summary>
    /// Create a new page extractor bound to a document.
    /// </summary>
    /// <param name="document">Target PDF document.</param>
    public PdfPageExtractor(IPdfDocumentInternal document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfPageExtractor>();
    }

    /// <summary>
    /// Extract all pages in the document populating <see cref="PdfDocument.Pages"/>
    /// RootRef is expected to be established earlier (xref parsing). This method will only set RootRef
    /// if it is currently unset (0) and a /Catalog is discovered during fallback scan.
    /// </summary>
    public void ExtractPages()
    {
        PdfPageLabelResolver labelResolver = null;
        if (_document.RootObject != null)
        {
            // Try to resolve page labels from the catalog
            labelResolver = new PdfPageLabelResolver(_document.RootObject.Dictionary);

            PdfObject rootPagesObject = _document.RootObject.Dictionary.GetObject(PdfTokens.PagesKey);
            if (rootPagesObject != null)
            {
                PdfPageResources initialResources = new();
                initialResources.UpdateFrom(rootPagesObject); // seed from root /Pages
                ExtractPagesFromPagesObject(rootPagesObject, 1, initialResources, labelResolver);
                return;
            }

            _logger.LogWarning("Root object (ref {RootRef}) present but /Pages tree not found.", _document.RootObject);
        }
        else
        {
            _logger.LogWarning("RootRef {RootRef} not found in objects.", _document.RootObject);
        }
    }

    /// <summary>
    /// Recursively extract pages from a /Pages node, handling nested page trees with inherited attributes.
    /// </summary>
    private int ExtractPagesFromPagesObject(PdfObject pagesObj, int currentPageNum, PdfPageResources inherited, PdfPageLabelResolver labelResolver)
    {
        if (pagesObj == null)
        {
            return currentPageNum;
        }

        // Clone and update for this level so siblings are isolated.
        PdfPageResources levelResources = inherited.Clone();
        levelResources.UpdateFrom(pagesObj);

        PdfArray kidsArray = pagesObj.Dictionary.GetValue(PdfTokens.KidsKey).AsArray();
        if (kidsArray == null)
        {
            _logger.LogWarning("/Pages node (ref {Ref}) missing /Kids array.", pagesObj.Reference.ObjectNumber);
            return currentPageNum;
        }

        for (int i = 0; i < kidsArray.Count; i++)
        {
            PdfObject kidObject = kidsArray.GetObject(i);
            if (kidObject == null)
            {
                _logger.LogWarning("Null kid reference at index {Index} in /Kids array of /Pages ref {Ref}.", i, pagesObj.Reference.ObjectNumber);
                continue;
            }

            PdfString typeName = kidObject.Dictionary.GetName(PdfTokens.TypeKey);
            if (typeName == PdfTokens.PageKey)
            {
                // Page-level overrides
                PdfPageResources pageResources = levelResources.Clone();
                pageResources.UpdateFrom(kidObject);
                PdfString pageLabel = labelResolver.GetLabel(currentPageNum - 1);
                PdfPage page = new(currentPageNum, pageLabel, _document, kidObject, pageResources);
                _document.Pages.Add(page);
                currentPageNum++;
            }
            else if (typeName == PdfTokens.PagesKey)
            {
                currentPageNum = ExtractPagesFromPagesObject(kidObject, currentPageNum, levelResources, labelResolver);
            }
            else
            {
                _logger.LogWarning("Unexpected /Type '{Type}' encountered in page tree (ref {Ref}).", typeName, kidObject.Reference.ObjectNumber);
            }
        }

        return currentPageNum;
    }
}
