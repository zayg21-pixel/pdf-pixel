using PdfPixel.Annotations.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Parsing;

/// <summary>
/// Resolves the destinations of a document. Named and indirect destinations are the ones links repeat,
/// so each is parsed once and handed out to every link leading to it.
/// </summary>
internal sealed class PdfDestinationResolver
{
    private readonly IPdfDocumentInternal _document;
    private readonly Dictionary<PdfString, PdfDestination?> _destinationsByName = [];
    private readonly Dictionary<PdfReference, PdfDestination?> _destinationsByReference = [];
    private Dictionary<PdfString, PdfDestinationReference>? _entriesByName;
    private Dictionary<PdfReference, IPdfPageInternal>? _pagesByReference;

    public PdfDestinationResolver(IPdfDocumentInternal document)
        => _document = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>
    /// Resolves a destination written at the place it is used.
    /// </summary>
    public PdfDestination? Resolve(in PdfDestinationReference destinationReference)
    {
        if (destinationReference.Destination != null)
        {
            return destinationReference.Destination;
        }

        if (destinationReference.Reference.IsValid)
        {
            return ResolveReference(destinationReference.Reference);
        }

        if (destinationReference.Name != null)
        {
            return ResolveNamed(destinationReference.Name.Value);
        }

        return null;
    }

    /// <summary>
    /// Resolves a destination by the name the catalog declares it under.
    /// </summary>
    public PdfDestination? ResolveNamed(in PdfString name)
    {
        if (_destinationsByName.TryGetValue(name, out PdfDestination? knownDestination))
        {
            return knownDestination;
        }

        if (_entriesByName == null)
        {
            _entriesByName = CollectNamedEntries();
        }

        PdfDestination? destination = null;

        if (_entriesByName.TryGetValue(name, out PdfDestinationReference entry))
        {
            destination = Resolve(entry);
        }

        _destinationsByName[name] = destination;

        return destination;
    }

    /// <summary>
    /// Gives the page a reference names, or null when the document has no such page.
    /// </summary>
    public IPdfPage? GetPage(in PdfReference pageReference)
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

    private PdfDestination? ResolveReference(in PdfReference reference)
    {
        if (_destinationsByReference.TryGetValue(reference, out PdfDestination? knownDestination))
        {
            return knownDestination;
        }

        PdfDestination? destination = null;
        PdfObject? destinationObject = _document.ObjectCache.GetObject(reference);

        if (destinationObject != null)
        {
            destination = ParseDestinationValue(destinationObject.Value.ResolveToNonReference(_document));
        }

        _destinationsByReference[reference] = destination;

        return destination;
    }

    private PdfDestination? ParseDestinationValue(IPdfValue? value)
    {
        PdfArray? destinationArray = value.AsArray();

        if (destinationArray == null)
        {
            // A destination object is often a dictionary carrying the destination under /D.
            PdfDictionary? destinationDictionary = value.AsDictionary();
            if (destinationDictionary != null)
            {
                destinationArray = destinationDictionary.GetArray(PdfTokens.DKey);
            }
        }

        if (destinationArray != null)
        {
            if (destinationArray.Count == 0)
            {
                return null;
            }

            return new PdfDestination(destinationArray);
        }

        PdfString? name = value.AsString();
        if (name != null)
        {
            return ResolveNamed(name.Value);
        }

        return null;
    }

    /// <summary>
    /// Collects the destination names the catalog declares, keeping the entries unresolved so that only
    /// the destinations actually used are parsed. <c>/Dests</c> wins over <c>/Names/Dests</c>.
    /// </summary>
    private Dictionary<PdfString, PdfDestinationReference> CollectNamedEntries()
    {
        PdfObject? rootObject = _document.RootObject;
        if (rootObject == null)
        {
            return new Dictionary<PdfString, PdfDestinationReference>();
        }

        PdfDictionary? destinationsTree = rootObject.Dictionary.GetDictionary(PdfTokens.NamesKey)?.GetDictionary(PdfTokens.DestsKey);
        Dictionary<PdfString, PdfDestinationReference> entries = (destinationsTree != null)
            ? _document.TreeReader.ReadNameTree(destinationsTree, PdfDestinationReference.FromArray)
            : new Dictionary<PdfString, PdfDestinationReference>();

        PdfDictionary? destinationsDictionary = rootObject.Dictionary.GetDictionary(PdfTokens.DestsKey);
        if (destinationsDictionary != null)
        {
            foreach (PdfString name in destinationsDictionary.RawValues.Keys)
            {
                entries[name] = PdfDestinationReference.FromDictionary(destinationsDictionary, name);
            }
        }

        return entries;
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
