using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Objects;
using PdfPixel.Resources;

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
    /// Extract all pages of the page tree of <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">The document catalog.</param>
    /// <exception cref="PdfInvalidDocumentException">Thrown if the catalog has no /Pages tree or no page is found.</exception>
    public List<IPdfPageInternal> ExtractPages(PdfDictionary catalog)
    {
        PdfObject? rootPagesObject = catalog.GetObject(PdfTokens.PagesKey);
        if (rootPagesObject == null)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: /Pages tree not found.");
        }

        // Try to resolve page labels from the catalog
        PdfPageLabelResolver labelResolver = new(catalog, _document.TreeReader);

        List<IPdfPageInternal> pages = [];
        PdfPageResources initialResources = new();
        initialResources.UpdateFrom(rootPagesObject); // seed from root /Pages
        HashSet<uint> visited = [];
        ExtractPagesFromPagesObject(rootPagesObject, 1, initialResources, labelResolver, visited, pages);

        if (pages.Count == 0)
        {
            throw new PdfInvalidDocumentException("Failed to parse PDF document: no pages found.");
        }

        return pages;
    }

    /// <summary>
    /// Recursively extract pages from a /Pages node, handling nested page trees with inherited attributes.
    /// </summary>
    private int ExtractPagesFromPagesObject(PdfObject pagesObj, int currentPageNum, PdfPageResources inherited, PdfPageLabelResolver labelResolver, HashSet<uint> visited, List<IPdfPageInternal> pages)
    {
        if (pagesObj == null)
        {
            return currentPageNum;
        }

        if (pagesObj.Reference != null && !visited.Add(pagesObj.Reference.Value.ObjectNumber))
        {
            _logger.LogWarning("Cycle detected in page tree at /Pages ref {Ref}; skipping.", pagesObj.Reference);
            return currentPageNum;
        }

        // Clone and update for this level so siblings are isolated.
        PdfPageResources levelResources = inherited.Clone();
        levelResources.UpdateFrom(pagesObj);

        PdfArray? kidsArray = pagesObj.Dictionary.GetValue(PdfTokens.KidsKey).AsArray();
        if (kidsArray == null)
        {
            _logger.LogWarning("/Pages node (ref {Ref}) missing /Kids array.", pagesObj.Reference);
            return currentPageNum;
        }

        for (int i = 0; i < kidsArray.Count; i++)
        {
            PdfObject? kidObject = kidsArray.GetObject(i);
            if (kidObject == null)
            {
                _logger.LogWarning("Null kid reference at index {Index} in /Kids array of /Pages ref {Ref}.", i, pagesObj.Reference);
                continue;
            }

            // A node is an intermediate /Pages node only when it actually carries kids of its own;
            // damaged documents routinely omit /Type on leaf pages, and inline them into /Kids.
            if (kidObject.Dictionary.GetName(PdfTokens.TypeKey) != PdfTokens.PageKey && kidObject.Dictionary.HasKey(PdfTokens.KidsKey))
            {
                currentPageNum = ExtractPagesFromPagesObject(kidObject, currentPageNum, levelResources, labelResolver, visited, pages);
            }
            else
            {
                // Page-level overrides
                PdfPageResources pageResources = levelResources.Clone();
                pageResources.UpdateFrom(kidObject);
                PdfString pageLabel = labelResolver.GetLabel(currentPageNum - 1);
                PdfPage page = new(currentPageNum, pageLabel, _document, kidObject, pageResources);
                pages.Add(page);
                currentPageNum++;
            }
        }

        return currentPageNum;
    }
}
