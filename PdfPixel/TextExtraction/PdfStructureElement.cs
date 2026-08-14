using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// A node in the document structure tree, corresponding to a PDF structure element (/StructElem).
/// Children are either nested structure elements or marked content references (MCIDs).
/// </summary>
public sealed class PdfStructureElement
{
    private readonly IPdfDocumentInternal _document;
    private IPdfPage? _cachedPage;

    private PdfStructureElement(IPdfDocumentInternal document)
        => _document = document;

    /// <summary>
    /// Pre-order index in the structure tree, assigned during tree construction.
    /// Used to determine reading order across structure elements.
    /// </summary>
    public int Index { get; internal set; }

    /// <summary>
    /// Parent structure element, or <see langword="null"/> for root elements.
    /// </summary>
    public PdfStructureElement? Parent { get; private set; }

    internal PdfReference? PageReference { get; private set; }

    /// <summary>
    /// Structure type tag (/S), e.g. "P", "Span", "H1", "Table".
    /// </summary>
    public PdfString Tag { get; internal set; }

    /// <summary>
    /// Language tag (/Lang), or empty if not specified.
    /// </summary>
    public PdfString? Lang { get; internal set; }

    /// <summary>
    /// Replacement text (/ActualText), or empty if not specified.
    /// </summary>
    public PdfString? ActualText { get; internal set; }

    /// <summary>
    /// Alternative text (/Alt), or empty if not specified.
    /// </summary>
    public PdfString? Alt { get; internal set; }

    /// <summary>
    /// Element identifier (/ID), or empty if not specified.
    /// </summary>
    public PdfString? Id { get; internal set; }

    /// <summary>
    /// Expanded form of an abbreviation (/E), or empty if not specified.
    /// </summary>
    public PdfString? ExpandedForm { get; internal set; }

    /// <summary>
    /// Title (/T), or empty if not specified.
    /// </summary>
    public PdfString? Title { get; internal set; }

    /// <summary>
    /// Revision number (/R), or <see langword="null"/> if not specified.
    /// </summary>
    public int? Revision { get; internal set; }

    /// <summary>
    /// Child structure elements and MCID references in document reading order.
    /// </summary>
    public List<PdfStructureNode> Children { get; } = [];

    /// <summary>
    /// Resolves the page associated with this structure element (/Pg),
    /// or <see langword="null"/> if no page reference is present.
    /// </summary>
    public IPdfPage? GetPage()
    {
        if (_cachedPage != null)
        {
            return _cachedPage;
        }

        if (PageReference == null)
        {
            return null;
        }

        for (int index = 0; index < _document.Pages.Count; index++)
        {
            if (_document.Pages[index].PageReference.Equals(PageReference.Value))
            {
                _cachedPage = _document.Pages[index];
                break;
            }
        }

        return _cachedPage;
    }

    /// <summary>
    /// Parses a structure element from a PDF dictionary.
    /// Returns <see langword="null"/> if the dictionary does not represent a valid structure element.
    /// </summary>
    internal static PdfStructureElement? FromDictionary(PdfDictionary dictionary, IPdfDocumentInternal document)
    {
        PdfString? tag = dictionary.GetName(PdfTokens.GroupSubtypeKey);
        if (tag == null || tag.Value.IsEmpty)
        {
            return null;
        }

        // TODO: [LOW] parse /C (class), /A (attributes)
        PdfStructureElement element = new(document)
        {
            PageReference = dictionary.GetReference(PdfTokens.PgKey),
            Tag = tag.Value,
            Lang = dictionary.GetString(PdfTokens.LangKey),
            ActualText = dictionary.GetString(PdfTokens.ActualTextKey),
            Alt = dictionary.GetString(PdfTokens.AltKey),
            Id = dictionary.GetString(PdfTokens.IdKey),
            ExpandedForm = dictionary.GetString(PdfTokens.ExpandedFormKey),
            Title = dictionary.GetString(PdfTokens.TitleKey),
            Revision = dictionary.GetInteger(PdfTokens.RKey)
        };

        ParseChildren(dictionary, document, element);

        return element;
    }

    private static void ParseChildren(PdfDictionary dictionary, IPdfDocumentInternal document, PdfStructureElement parent)
    {
        PdfArray? childrenArray = dictionary.GetArray(PdfTokens.KKey);
        if (childrenArray != null)
        {
            for (int i = 0; i < childrenArray.Count; i++)
            {
                int? mcid = childrenArray.GetInteger(i);
                if (mcid != null)
                {
                    parent.Children.Add(PdfStructureNode.FromMcid(mcid.Value));
                    continue;
                }

                PdfDictionary? kidDict = childrenArray.GetDictionary(i);
                if (kidDict != null)
                {
                    ParseKidDictionary(kidDict, document, parent);
                }
            }

            return;
        }

        int? singleMcid = dictionary.GetInteger(PdfTokens.KKey);
        if (singleMcid != null)
        {
            parent.Children.Add(PdfStructureNode.FromMcid(singleMcid.Value));
            return;
        }

        PdfDictionary? singleKidDict = dictionary.GetDictionary(PdfTokens.KKey);
        if (singleKidDict != null)
        {
            ParseKidDictionary(singleKidDict, document, parent);
        }
    }

    private static void ParseKidDictionary(PdfDictionary kidDict, IPdfDocumentInternal document, PdfStructureElement parent)
    {
        int? mcid = kidDict.GetInteger(PdfTokens.MCIDKey);
        if (mcid != null)
        {
            parent.Children.Add(PdfStructureNode.FromMcid(mcid.Value, kidDict.GetReference(PdfTokens.PgKey)));
            return;
        }

        PdfStructureElement? child = FromDictionary(kidDict, document);
        if (child != null)
        {
            child.Parent = parent;
            parent.Children.Add(PdfStructureNode.FromElement(child));
        }
    }
}
