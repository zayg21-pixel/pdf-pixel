using PdfPixel.Models;
using PdfPixel.Text;
using System;

namespace PdfPixel.Parsing;

/// <summary>
/// Parses named destinations from the PDF document catalog.
/// Handles both the older /Dests dictionary and the newer /Names/Dests name tree.
/// </summary>
internal class PdfNamedDestinationParser
{
    private readonly IPdfDocumentInternal _document;

    public PdfNamedDestinationParser(IPdfDocumentInternal document)
        => _document = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>
    /// Parses named destinations from the document catalog.
    /// Returns the resolved destinations dictionary, or <c>null</c> if none found.
    /// </summary>
    public PdfDictionary? ParseNamedDestinations()
    {
        PdfObject? rootObject = _document.RootObject;
        if (rootObject == null)
        {
            return null;
        }

        PdfDictionary catalogDict = rootObject.Dictionary;

        // Try older /Dests dictionary first.
        PdfDictionary? destsDict = catalogDict.GetDictionary(PdfTokens.DestsKey);
        if (destsDict != null)
        {
            return destsDict;
        }

        // Fall back to newer /Names/Dests name tree.
        return ParseNamesTree(catalogDict);
    }

    private PdfDictionary? ParseNamesTree(PdfDictionary catalogDict)
    {
        PdfDictionary? namesDict = catalogDict.GetDictionary(PdfTokens.NamesKey);
        if (namesDict == null)
        {
            return null;
        }

        PdfDictionary? destsTreeRoot = namesDict.GetDictionary(PdfTokens.DestsKey);
        if (destsTreeRoot == null)
        {
            return null;
        }

        PdfDictionary flattenedDict = new(_document);
        FlattenNameTree(destsTreeRoot, flattenedDict);

        if (flattenedDict.Count > 0)
        {
            return flattenedDict;
        }

        return null;
    }

    private static void FlattenNameTree(PdfDictionary node, PdfDictionary target)
    {
        PdfArray? namesArray = node.GetArray(PdfTokens.NamesKey);
        if (namesArray != null)
        {
            for (int i = 0; i < namesArray.Count - 1; i += 2)
            {
                PdfString? name = namesArray.GetString(i);
                IPdfValue? value = namesArray.GetValue(i + 1);
                if (name?.IsEmpty == false && value != null)
                {
                    target.Set(name.Value, value);
                }
            }
        }

        PdfArray? kidsArray = node.GetArray(PdfTokens.KidsKey);
        if (kidsArray != null)
        {
            for (int i = 0; i < kidsArray.Count; i++)
            {
                PdfDictionary? kidDict = kidsArray.GetDictionary(i);
                if (kidDict != null)
                {
                    FlattenNameTree(kidDict, target);
                }
            }
        }
    }
}
