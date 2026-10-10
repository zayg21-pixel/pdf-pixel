using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Parsing;

/// <summary>
/// Reads name trees and number trees into maps from key to value.
/// </summary>
internal sealed class PdfTreeReader
{
    private const int DefaultMaxDepth = 10;

    private readonly ILogger<PdfTreeReader> _logger;
    private readonly int _maxDepth;

    /// <summary>
    /// Initializes a reader that reads /Kids up to <paramref name="maxDepth"/> levels below the root.
    /// </summary>
    public PdfTreeReader(ILogger<PdfTreeReader> logger, int maxDepth = DefaultMaxDepth)
    {
        _logger = logger;
        _maxDepth = maxDepth;
    }

    /// <summary>
    /// Reads every /Names entry of the name tree under <paramref name="root"/>. <paramref name="factory"/>
    /// creates each value from the /Names array and the value's index; entries it returns
    /// <see langword="null"/> for are skipped. A later duplicate key replaces an earlier one.
    /// </summary>
    /// <param name="root">Root node of the tree.</param>
    /// <param name="factory">Creates the value at an index of a /Names array.</param>
    /// <param name="maxDepth">Levels of /Kids read below the root, or <see langword="null"/> for the reader's limit.</param>
    public Dictionary<PdfString, T> ReadNameTree<T>(PdfDictionary root, Func<PdfArray, int, T?> factory, int? maxDepth = null)
    {
        Dictionary<PdfString, T> entries = [];
        HashSet<PdfReference> visitedNodes = [];
        ReadNode(root, PdfTokens.NamesKey, 0, maxDepth ?? _maxDepth, visitedNodes, ReadEntries);

        return entries;

        void ReadEntries(PdfArray names)
        {
            WarnOnUnpairedEntry(names);

            for (int index = 0; index + 1 < names.Count; index += 2)
            {
                PdfString? key = names.GetString(index);
                T? value = factory(names, index + 1);
                if (key == null || value == null)
                {
                    continue;
                }

                if (entries.ContainsKey(key.Value))
                {
                    _logger.LogWarning("Name tree has duplicate key {Key}; the later entry is used.", key.Value);
                }

                entries[key.Value] = value;
            }
        }
    }

    /// <summary>
    /// Reads every /Nums entry of the number tree under <paramref name="root"/>. <paramref name="factory"/>
    /// creates each value from the /Nums array and the value's index; entries it returns
    /// <see langword="null"/> for are skipped. A later duplicate key replaces an earlier one.
    /// </summary>
    /// <param name="root">Root node of the tree.</param>
    /// <param name="factory">Creates the value at an index of a /Nums array.</param>
    /// <param name="maxDepth">Levels of /Kids read below the root, or <see langword="null"/> for the reader's limit.</param>
    public Dictionary<int, T> ReadNumberTree<T>(PdfDictionary root, Func<PdfArray, int, T?> factory, int? maxDepth = null)
    {
        Dictionary<int, T> entries = [];
        HashSet<PdfReference> visitedNodes = [];
        ReadNode(root, PdfTokens.NumsKey, 0, maxDepth ?? _maxDepth, visitedNodes, ReadEntries);

        return entries;

        void ReadEntries(PdfArray numbers)
        {
            WarnOnUnpairedEntry(numbers);

            for (int index = 0; index + 1 < numbers.Count; index += 2)
            {
                int? key = numbers.GetInteger(index);
                T? value = factory(numbers, index + 1);
                if (key == null || value == null)
                {
                    continue;
                }

                if (entries.ContainsKey(key.Value))
                {
                    _logger.LogWarning("Number tree has duplicate key {Key}; the later entry is used.", key.Value);
                }

                entries[key.Value] = value;
            }
        }
    }

    private void ReadNode(PdfDictionary node, in PdfString entriesKey, int depth, int maxDepth, HashSet<PdfReference> visitedNodes, Action<PdfArray> readEntries)
    {
        PdfArray? entries = node.GetArray(entriesKey);
        if (entries != null)
        {
            readEntries(entries);
        }

        PdfArray? kids = node.GetArray(PdfTokens.KidsKey);
        if (kids == null)
        {
            return;
        }

        if (depth >= maxDepth)
        {
            _logger.LogWarning("Name or number tree is deeper than {MaxDepth} levels; deeper nodes are skipped.", maxDepth);
            return;
        }

        for (int index = 0; index < kids.Count; index++)
        {
            PdfReference? kidReference = kids.GetReference(index);
            if (kidReference != null && !visitedNodes.Add(kidReference.Value))
            {
                _logger.LogWarning("Name or number tree node {Reference} is referenced more than once; it is read once.", kidReference.Value);
                continue;
            }

            PdfDictionary? kid = kids.GetDictionary(index);
            if (kid != null)
            {
                ReadNode(kid, entriesKey, depth + 1, maxDepth, visitedNodes, readEntries);
            }
        }
    }

    private void WarnOnUnpairedEntry(PdfArray entries)
    {
        if (entries.Count % 2 != 0)
        {
            _logger.LogWarning("Name or number tree leaf has an odd number of entries; the last key has no value.");
        }
    }
}
