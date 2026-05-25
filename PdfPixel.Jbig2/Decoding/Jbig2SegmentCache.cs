using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Holds all decoded JBIG2 segment artifacts accumulated during stream processing.
/// Provides typed accessors and helpers for cross-segment reference resolution.
/// </summary>
public sealed class Jbig2SegmentCache
{
    private readonly Dictionary<uint, Jbig2SegmentHeader> _segmentMap = [];
    private readonly Dictionary<uint, Jbig2Bitmap[]> _symbolDictionaries = [];
    private readonly Dictionary<uint, Jbig2Bitmap[]> _patternDictionaries = [];
    private readonly Dictionary<uint, Jbig2HuffmanTable> _userTables = [];
    private readonly Dictionary<uint, Jbig2Bitmap> _intermediateRegions = [];

    /// <summary>
    /// Registers a parsed segment header.
    /// </summary>
    public void AddSegment(Jbig2SegmentHeader segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        _segmentMap[segment.SegmentNumber] = segment;
    }

    /// <summary>
    /// Stores a decoded symbol dictionary under the given segment number.
    /// </summary>
    public void AddSymbolDictionary(uint segmentNumber, Jbig2Bitmap[] symbols) => _symbolDictionaries[segmentNumber] = symbols;

    /// <summary>
    /// Collects all symbol bitmaps from segments referred to by <paramref name="segment"/>,
    /// in referral order.
    /// </summary>
    public List<Jbig2Bitmap> CollectReferredSymbols(Jbig2SegmentHeader segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        List<Jbig2Bitmap> symbols = [];

        foreach (uint refSegNum in segment.ReferredToSegments)
        {
            if (_symbolDictionaries.TryGetValue(refSegNum, out Jbig2Bitmap[]? dictSymbols))
            {
                symbols.AddRange(dictSymbols);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Stores a decoded pattern dictionary under the given segment number.
    /// </summary>
    public void AddPatternDictionary(uint segmentNumber, Jbig2Bitmap[] patterns) => _patternDictionaries[segmentNumber] = patterns;

    /// <summary>
    /// Returns the pattern array from the first referred-to pattern dictionary segment,
    /// or <c>null</c> if none is found.
    /// </summary>
    public Jbig2Bitmap[]? CollectReferredPatterns(Jbig2SegmentHeader segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        foreach (uint refSegNum in segment.ReferredToSegments)
        {
            if (_patternDictionaries.TryGetValue(refSegNum, out Jbig2Bitmap[]? refPatterns))
            {
                return refPatterns;
            }
        }

        return null;
    }

    /// <summary>
    /// Stores a parsed user-defined Huffman table under the given segment number.
    /// </summary>
    public void AddUserTable(uint segmentNumber, Jbig2HuffmanTable table) => _userTables[segmentNumber] = table;

    /// <summary>
    /// Collects user-defined Huffman tables from referred-to segments, in referral order.
    /// Only segments that are Table segments (and present in the cache) are returned.
    /// Used by symbol dictionary and text region decoders to resolve custom table selections.
    /// </summary>
    public List<Jbig2HuffmanTable> CollectReferredUserTables(Jbig2SegmentHeader segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        List<Jbig2HuffmanTable> tables = [];

        foreach (uint refSegNum in segment.ReferredToSegments)
        {
            if (_userTables.TryGetValue(refSegNum, out Jbig2HuffmanTable? table))
            {
                tables.Add(table);
            }
        }

        return tables;
    }

    /// <summary>
    /// Stores an intermediate region bitmap for later reference by other segments.
    /// </summary>
    public void AddIntermediateRegion(uint segmentNumber, Jbig2Bitmap bitmap) => _intermediateRegions[segmentNumber] = bitmap;

    /// <summary>
    /// Resolves the primary reference bitmap for a refinement region from the segment's
    /// referred-to list: checks intermediate regions first, then falls back to the first
    /// symbol in a referred symbol dictionary.
    /// Returns <c>null</c> when no referred segment can supply a bitmap.
    /// </summary>
    public Jbig2Bitmap? ResolveReferenceBitmap(Jbig2SegmentHeader segment)
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        if (segment.ReferredToSegments.Length == 0)
        {
            return null;
        }

        uint refSegNum = segment.ReferredToSegments[0];

        if (_intermediateRegions.TryGetValue(refSegNum, out Jbig2Bitmap? refBitmap))
        {
            return refBitmap;
        }

        if (_symbolDictionaries.TryGetValue(refSegNum, out Jbig2Bitmap[]? refSymbols) && refSymbols.Length > 0)
        {
            return refSymbols[0];
        }

        return null;
    }

    /// <summary>
    /// Merges all entries from <paramref name="source"/> into this cache.
    /// Existing entries with the same segment number are overwritten.
    /// Used to incorporate globals into a page-level cache.
    /// </summary>
    public void MergeFrom(Jbig2SegmentCache? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (KeyValuePair<uint, Jbig2SegmentHeader> pair in source._segmentMap)
        {
            _segmentMap[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<uint, Jbig2Bitmap[]> pair in source._symbolDictionaries)
        {
            _symbolDictionaries[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<uint, Jbig2Bitmap[]> pair in source._patternDictionaries)
        {
            _patternDictionaries[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<uint, Jbig2HuffmanTable> pair in source._userTables)
        {
            _userTables[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<uint, Jbig2Bitmap> pair in source._intermediateRegions)
        {
            _intermediateRegions[pair.Key] = pair.Value;
        }
    }
}
