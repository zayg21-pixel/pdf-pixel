using System;
using System.Collections.Generic;
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
    /// Extract all pages in the document populating <see cref="IPdfDocument.Pages"/>.
    /// RootRef is expected to be established earlier (xref parsing). This method will only set RootRef
    /// if it is currently unset (0) and a /Catalog is discovered during fallback scan.
    /// </summary>
    public void ExtractPages()
    {
        PdfObject? rootObject = _document.RootObject;
        if (rootObject == null)
        {
            _logger.LogWarning("Document root not found in objects.");
            return;
        }

        PdfObject? rootPagesObject = rootObject.Dictionary.GetObject(PdfTokens.PagesKey);
        if (rootPagesObject == null)
        {
            _logger.LogWarning("Root object (ref {RootRef}) present but /Pages tree not found; attempting recovery scan.", rootObject.Reference);
            _document.ObjectCache.RunRecoveryScan();

            // The recovery scan picks its own root, so the /Pages tree has to be read off the new one.
            rootObject = _document.RootObject;
            rootPagesObject = rootObject?.Dictionary.GetObject(PdfTokens.PagesKey);
        }

        if (rootObject == null || rootPagesObject == null)
        {
            _logger.LogWarning("Document root present but /Pages tree not found even after recovery scan.");
            return;
        }

        // Try to resolve page labels from the catalog
        PdfPageLabelResolver labelResolver = new(rootObject.Dictionary);

        PdfPageResources initialResources = new();
        initialResources.UpdateFrom(rootPagesObject); // seed from root /Pages
        HashSet<uint> visited = [];
        ExtractPagesFromPagesObject(rootPagesObject, 1, initialResources, labelResolver, visited);
    }

    /// <summary>
    /// Recursively extract pages from a /Pages node, handling nested page trees with inherited attributes.
    /// </summary>
    private int ExtractPagesFromPagesObject(PdfObject pagesObj, int currentPageNum, PdfPageResources inherited, PdfPageLabelResolver labelResolver, HashSet<uint> visited)
    {
        if (pagesObj == null)
        {
            return currentPageNum;
        }

        if (!visited.Add(pagesObj.Reference.ObjectNumber))
        {
            _logger.LogWarning("Cycle detected in page tree at /Pages ref {Ref}; skipping.", pagesObj.Reference.ObjectNumber);
            return currentPageNum;
        }

        // Clone and update for this level so siblings are isolated.
        PdfPageResources levelResources = inherited.Clone();
        levelResources.UpdateFrom(pagesObj);

        PdfArray? kidsArray = pagesObj.Dictionary.GetValue(PdfTokens.KidsKey).AsArray();
        if (kidsArray == null)
        {
            _logger.LogWarning("/Pages node (ref {Ref}) missing /Kids array.", pagesObj.Reference.ObjectNumber);
            return currentPageNum;
        }

        for (int i = 0; i < kidsArray.Count; i++)
        {
            PdfObject? kidObject = kidsArray.GetObject(i);
            if (kidObject == null)
            {
                _logger.LogWarning("Null kid reference at index {Index} in /Kids array of /Pages ref {Ref}.", i, pagesObj.Reference.ObjectNumber);
                continue;
            }

            // A node is an intermediate /Pages node only when it actually carries kids of its own;
            // damaged documents routinely omit /Type on leaf pages, and inline them into /Kids.
            PdfString typeName = kidObject.Dictionary.GetName(PdfTokens.TypeKey);
            if (typeName != PdfTokens.PageKey && kidObject.Dictionary.HasKey(PdfTokens.KidsKey))
            {
                currentPageNum = ExtractPagesFromPagesObject(kidObject, currentPageNum, levelResources, labelResolver, visited);
            }
            else
            {
                // Page-level overrides
                PdfPageResources pageResources = levelResources.Clone();
                pageResources.UpdateFrom(kidObject);
                PdfString pageLabel = labelResolver.GetLabel(currentPageNum - 1);
                PdfPage page = new(currentPageNum, pageLabel, _document, kidObject, pageResources);
                _document.Pages.Add(page);
                currentPageNum++;
            }
        }

        return currentPageNum;
    }
}
