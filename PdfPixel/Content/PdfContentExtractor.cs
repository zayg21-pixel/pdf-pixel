using PdfPixel.Content.Model;
using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Tagging.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Content;

/// <summary>
/// Extracts the content items of a content tree in content stream order, with the /Alt, /ActualText and /AF
/// of each item or of the content enclosing it.
/// </summary>
public sealed class PdfContentExtractor
{
    private readonly List<PdfContentEntry> _entries = [];

    /// <summary>
    /// Extracts the content items of the content tree under <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root of the content tree to extract.</param>
    /// <param name="parameters">Parameters controlling the extraction.</param>
    public PdfContentEntry[] ExtractContent(PdfContentNode root, PdfContentExtractionParameters parameters)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _entries.Clear();
        CollectEntries(root, parameters, null, null, null);

        return _entries.ToArray();
    }

    private void CollectEntries(
        PdfContentNode node,
        PdfContentExtractionParameters parameters,
        PdfString? alternateDescription,
        PdfString? actualText,
        IReadOnlyList<PdfFileSpecification>? associatedFiles)
    {
        foreach (PdfContentElement element in node.Elements)
        {
            switch (element.ElementType)
            {
                case PdfContentElementType.Node:
                {
                    CollectNodeEntries((PdfContentNode)element, parameters, alternateDescription, actualText, associatedFiles);
                    break;
                }
                case PdfContentElementType.Item:
                {
                    var item = (PdfContentItem)element;

                    if (parameters.ContentKinds == null || parameters.ContentKinds.Contains(item.Command.ContentKind))
                    {
                        _entries.Add(new PdfContentEntry(item, alternateDescription, actualText, associatedFiles));
                    }

                    break;
                }
            }
        }
    }

    private void CollectNodeEntries(
        PdfContentNode node,
        PdfContentExtractionParameters parameters,
        PdfString? alternateDescription,
        PdfString? actualText,
        IReadOnlyList<PdfFileSpecification>? associatedFiles)
    {
        switch (node.Scope)
        {
            case PdfContentStreamScope:
            {
                if (!PdfContentExtractionUtilities.IsExcludedContentStream(node.Scope, parameters))
                {
                    CollectEntries(node, parameters, null, null, null);
                }

                break;
            }
            case PdfContentScope contentScope:
            {
                CollectEntries(
                    node,
                    parameters,
                    PdfContentExtractionUtilities.GetAlternateDescription(contentScope) ?? alternateDescription,
                    PdfContentExtractionUtilities.GetActualText(contentScope) ?? actualText,
                    contentScope.AssociatedFiles ?? associatedFiles);
                break;
            }
            default:
            {
                CollectEntries(node, parameters, alternateDescription, actualText, associatedFiles);
                break;
            }
        }
    }
}
