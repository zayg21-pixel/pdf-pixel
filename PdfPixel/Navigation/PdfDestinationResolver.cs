using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Navigation;

/// <summary>
/// Resolves the destinations of a document into the targets a viewer navigates to. Destinations written
/// indirectly or by name are parsed once and handed out to every owner leading to them.
/// </summary>
public sealed class PdfDestinationResolver
{
    private readonly IPdfDocumentInternal _document;
    private readonly Dictionary<PdfReference, PdfDestination?> _destinationsByReference = [];
    private readonly Dictionary<PdfString, PdfDestination?> _destinationsByName = [];
    private bool _namedEntriesCollected;
    private PdfDictionary? _destinationsDictionary;
    private Dictionary<PdfString, (PdfArray Names, int Index)>? _nameTreeEntries;
    private Dictionary<PdfReference, IPdfPageInternal>? _pagesByReference;

    internal PdfDestinationResolver(IPdfDocumentInternal document)
        => _document = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>
    /// Resolves a destination of this document into the target to navigate to, or null when it leads
    /// nowhere in this document.
    /// </summary>
    /// <param name="destination">Destination belonging to this document.</param>
    public PdfNavigationTarget? Resolve(PdfDestination destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        PdfDestination? current = destination;
        HashSet<PdfString> visitedNames = [];

        while (current is PdfNamedDestination namedDestination)
        {
            if (!visitedNames.Add(namedDestination.Name))
            {
                return null;
            }

            current = GetNamedDestination(namedDestination.Name);
        }

        if (current is not PdfExplicitDestination explicitDestination)
        {
            return null;
        }

        int? pageIndex = GetPageIndex(explicitDestination);
        if (pageIndex == null)
        {
            return null;
        }

        return new PdfNavigationTarget(pageIndex.Value, explicitDestination);
    }

    /// <summary>
    /// Reads the destination a dictionary holds under a key: a destination array, a name, or a reference
    /// to an object holding either.
    /// </summary>
    internal PdfDestination? Parse(PdfDictionary dictionary, in PdfString key)
    {
        PdfReference? reference = dictionary.GetReference(key);
        if (reference != null)
        {
            return ParseReference(reference.Value);
        }

        return ParseValue(dictionary.GetValue(key));
    }

    /// <summary>
    /// Reads the destination an array holds at an index: a destination array, a name, or a reference
    /// to an object holding either.
    /// </summary>
    internal PdfDestination? Parse(PdfArray array, int index)
    {
        PdfReference? reference = array.GetReference(index);
        if (reference != null)
        {
            return ParseReference(reference.Value);
        }

        return ParseValue(array.GetValue(index));
    }

    /// <summary>
    /// Reads the structure destination (/SD) a dictionary holds under a key: a structure element
    /// reference or a structure element ID, followed by how the target page is displayed.
    /// </summary>
    internal static PdfExplicitDestination? ParseStructure(PdfDictionary dictionary, in PdfString key)
    {
        PdfArray? destinationArray = dictionary.GetArray(key);
        if (destinationArray == null || destinationArray.Count == 0)
        {
            return null;
        }

        PdfReference? elementReference = destinationArray.GetReference(0);
        if (elementReference != null)
        {
            return new PdfStructureDestination(destinationArray, elementReference.Value);
        }

        PdfString? elementId = destinationArray.GetString(0);
        if (elementId != null)
        {
            return new PdfStructureIdDestination(destinationArray, elementId.Value);
        }

        return null;
    }

    private static PdfExplicitDestination? ParseExplicit(PdfArray destinationArray)
    {
        if (destinationArray.Count == 0)
        {
            return null;
        }

        PdfReference? pageReference = destinationArray.GetReference(0);
        if (pageReference?.IsValid == true)
        {
            return new PdfPageDestination(destinationArray, pageReference.Value);
        }

        int? pageNumber = destinationArray.GetInteger(0);
        if (pageNumber != null)
        {
            return new PdfPageNumberDestination(destinationArray, pageNumber.Value);
        }

        return null;
    }

    /// <summary>
    /// Finds the page holding the first content of a structure element: the kids are taken in order,
    /// descending into child elements, and the page of the first content item that names one is used.
    /// </summary>
    private static PdfReference? FindFirstContentPage(PdfStructureElement element, HashSet<PdfStructureElement> visitedElements)
    {
        if (!visitedElements.Add(element))
        {
            return null;
        }

        foreach (IPdfStructureNode child in element.EnumerateChildren())
        {
            if (child is PdfStructureElement childElement)
            {
                PdfReference? childPage = FindFirstContentPage(childElement, visitedElements);
                if (childPage != null)
                {
                    return childPage;
                }
            }
            else if (child.PageReference != null)
            {
                return child.PageReference;
            }
        }

        return null;
    }

    private PdfDestination? ParseReference(in PdfReference reference)
    {
        if (_destinationsByReference.TryGetValue(reference, out PdfDestination? knownDestination))
        {
            return knownDestination;
        }

        // Registered before parsing so that an object leading back to itself ends here.
        _destinationsByReference[reference] = null;

        PdfDestination? destination = null;
        PdfObject? destinationObject = _document.ObjectCache.GetObject(reference);

        if (destinationObject != null)
        {
            destination = ParseValue(destinationObject.Value.ResolveToNonReference(_document));
        }

        _destinationsByReference[reference] = destination;

        return destination;
    }

    private PdfDestination? ParseValue(IPdfValue? value)
    {
        PdfArray? destinationArray = value.AsArray();
        if (destinationArray != null)
        {
            return ParseExplicit(destinationArray);
        }

        // A destination object is often a dictionary carrying the destination under /D.
        PdfDictionary? destinationDictionary = value.AsDictionary();
        if (destinationDictionary != null)
        {
            return Parse(destinationDictionary, PdfTokens.DKey);
        }

        PdfString? name = value.AsString();
        if (name != null)
        {
            return new PdfNamedDestination(name.Value);
        }

        return null;
    }

    private int? GetPageIndex(PdfExplicitDestination destination)
    {
        switch (destination)
        {
            case PdfPageDestination pageDestination:
            {
                IPdfPageInternal? page = GetPage(pageDestination.PageReference);
                if (page == null)
                {
                    return null;
                }

                return page.PageNumber - 1;
            }
            case PdfPageNumberDestination pageNumberDestination:
            {
                if (pageNumberDestination.PageNumber < 0 || pageNumberDestination.PageNumber >= _document.Pages.Count)
                {
                    return null;
                }

                return pageNumberDestination.PageNumber;
            }
            case PdfStructureDestination structureDestination:
            {
                PdfStructureTree? structureTree = _document.StructureTree;
                PdfObject? elementObject = _document.ObjectCache.GetObject(structureDestination.ElementReference);
                if (structureTree == null || elementObject == null)
                {
                    return null;
                }

                PdfStructureElement element = new(elementObject.Dictionary, structureDestination.ElementReference, structureTree);

                return GetStructureElementPageIndex(element);
            }
            case PdfStructureIdDestination structureIdDestination:
            {
                PdfStructureElement? element = _document.StructureTree?.FindById(structureIdDestination.ElementId);
                if (element == null)
                {
                    return null;
                }

                return GetStructureElementPageIndex(element);
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Index of the page holding the element's first content, or of the first page when the element
    /// has no content naming a page of this document.
    /// </summary>
    private int GetStructureElementPageIndex(PdfStructureElement element)
    {
        PdfReference? pageReference = FindFirstContentPage(element, []);
        if (pageReference != null)
        {
            IPdfPageInternal? page = GetPage(pageReference.Value);
            if (page != null)
            {
                return page.PageNumber - 1;
            }
        }

        return 0;
    }

    private PdfDestination? GetNamedDestination(in PdfString name)
    {
        if (_destinationsByName.TryGetValue(name, out PdfDestination? knownDestination))
        {
            return knownDestination;
        }

        if (!_namedEntriesCollected)
        {
            CollectNamedEntries();
            _namedEntriesCollected = true;
        }

        PdfDestination? destination = null;

        if (_destinationsDictionary != null && _destinationsDictionary.HasKey(name))
        {
            destination = Parse(_destinationsDictionary, name);
        }
        else if (_nameTreeEntries != null && _nameTreeEntries.TryGetValue(name, out (PdfArray Names, int Index) entry))
        {
            destination = Parse(entry.Names, entry.Index);
        }

        _destinationsByName[name] = destination;

        return destination;
    }

    /// <summary>
    /// Collects where the catalog declares its destination names, keeping the entries unparsed so that
    /// only the destinations actually used are parsed. <c>/Dests</c> wins over <c>/Names/Dests</c>.
    /// </summary>
    private void CollectNamedEntries()
    {
        PdfObject? rootObject = _document.RootObject;
        if (rootObject == null)
        {
            return;
        }

        PdfDictionary? destinationsTree = rootObject.Dictionary.GetDictionary(PdfTokens.NamesKey)?.GetDictionary(PdfTokens.DestsKey);
        if (destinationsTree != null)
        {
            _nameTreeEntries = _document.TreeReader.ReadNameTree(destinationsTree, GetNameTreeEntry);
        }

        _destinationsDictionary = rootObject.Dictionary.GetDictionary(PdfTokens.DestsKey);
    }

    private static (PdfArray Names, int Index) GetNameTreeEntry(PdfArray names, int index) => (names, index);

    private IPdfPageInternal? GetPage(in PdfReference pageReference)
    {
        if (_pagesByReference == null)
        {
            _pagesByReference = BuildPageIndex();
        }

        if (_pagesByReference.TryGetValue(pageReference, out IPdfPageInternal? page))
        {
            return page;
        }

        return null;
    }

    /// <summary>
    /// Indexes the pages by their reference, so that the page of a destination takes one lookup instead
    /// of a scan over every page.
    /// </summary>
    private Dictionary<PdfReference, IPdfPageInternal> BuildPageIndex()
    {
        List<IPdfPageInternal> pages = _document.Pages;
        Dictionary<PdfReference, IPdfPageInternal> pagesByReference = new(pages.Count);

        foreach (IPdfPageInternal page in pages)
        {
            if (page.PageReference.IsValid && !pagesByReference.ContainsKey(page.PageReference))
            {
                pagesByReference.Add(page.PageReference, page);
            }
        }

        return pagesByReference;
    }
}
