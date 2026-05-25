using Microsoft.Extensions.Logging;
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
    private readonly ILogger<PdfNamedDestinationParser> _logger;

    public PdfNamedDestinationParser(IPdfDocumentInternal document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = document.LoggerFactory.CreateLogger<PdfNamedDestinationParser>();
    }

    /// <summary>
    /// Parses named destinations from the document catalog and populates <see cref="IPdfDocumentInternal.NamedDestinations"/>.
    /// </summary>
    public void ParseNamedDestinations()
    {
        if (_document.RootObject == null)
        {
            return;
        }

        PdfDictionary catalogDict = _document.RootObject.Dictionary;

        ParseDestsDictionary(catalogDict);
        ParseNamesTree(catalogDict);
    }

    private void ParseDestsDictionary(PdfDictionary catalogDict)
    {
        PdfDictionary destsDict = catalogDict.GetDictionary(PdfTokens.DestsKey);
        if (destsDict != null)
        {
            _document.NamedDestinations = destsDict;
            _logger.LogDebug("Found /Dests dictionary with {Count} entries.", destsDict.Count);
        }
    }

    private void ParseNamesTree(PdfDictionary catalogDict)
    {
        PdfDictionary namesDict = catalogDict.GetDictionary(PdfTokens.NamesKey);
        if (namesDict == null)
        {
            return;
        }

        PdfDictionary destsTreeRoot = namesDict.GetDictionary(PdfTokens.DestsKey);
        if (destsTreeRoot == null)
        {
            return;
        }

        PdfDictionary flattenedDict = new(_document);
        FlattenNameTree(destsTreeRoot, flattenedDict);

        if (flattenedDict.Count > 0)
        {
            _document.NamedDestinations = flattenedDict;
            _logger.LogDebug("Flattened /Names/Dests tree into {Count} entries.", flattenedDict.Count);
        }
    }

    private static void FlattenNameTree(PdfDictionary node, PdfDictionary target)
    {
        PdfArray namesArray = node.GetArray(PdfTokens.NamesKey);
        if (namesArray != null)
        {
            for (int i = 0; i < namesArray.Count - 1; i += 2)
            {
                PdfString name = namesArray.GetString(i);
                IPdfValue value = namesArray.GetValue(i + 1);
                if (!name.IsEmpty && value != null)
                {
                    target.Set(name, value);
                }
            }
        }

        PdfArray kidsArray = node.GetArray(PdfTokens.KidsKey);
        if (kidsArray != null)
        {
            for (int i = 0; i < kidsArray.Count; i++)
            {
                PdfDictionary kidDict = kidsArray.GetDictionary(i);
                if (kidDict != null)
                {
                    FlattenNameTree(kidDict, target);
                }
            }
        }
    }
}
