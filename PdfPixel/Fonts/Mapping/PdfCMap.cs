using PdfPixel.Objects;
using PdfPixel.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// General CMap for character mapping (length-aware, character code-based).
/// Stores both code-to-CID and code-to-Unicode mappings for full CMap support.
/// </summary>
public class PdfCMap
{
    /// <summary>
    /// The longest /UseCMap chain followed when loading an embedded CMap stream.
    /// </summary>
    private const int MaxUseCMapDepth = 8;

    private readonly Dictionary<PdfCharacterCode, string> _characterCodeToUnicode = [];
    private readonly Dictionary<PdfCharacterCode, int> _codeToCid = [];

    private readonly LengthBuckets<UnicodeRangeMap> _unicodeRangesByLength = new();
    private readonly LengthBuckets<CidRangeMap> _cidRangesByLength = new();

    private readonly List<CodeSpaceRange> _codeSpaceRanges = [];

    /// <summary>
    /// True if any codespace ranges are present.
    /// </summary>
    public bool HasCodeSpaceRanges => _codeSpaceRanges.Count > 0;

    /// <summary>
    /// True when this CMap has at least one code-to-Unicode mapping declared via <c>bfchar</c>/<c>bfrange</c>.
    /// A CMap parsed from a stream that declares only <c>cidchar</c>/<c>cidrange</c> entries (a code/CID
    /// mapping mislabeled as <c>/ToUnicode</c>) has none, even though it isn't otherwise empty.
    /// </summary>
    public bool HasUnicodeMappings => _characterCodeToUnicode.Count > 0 || !IsRangeBucketsEmpty(_unicodeRangesByLength);

    /// <summary>
    /// A read-only view of the codespace ranges declared on this CMap.
    /// </summary>
    public IReadOnlyList<CodeSpaceRange> CodeSpaceRanges => _codeSpaceRanges;

    /// <summary>
    /// True when this CMap has no codespace ranges and no character mappings of any kind.
    /// </summary>
    internal bool IsEmpty
    {
        get
        {
            return !HasCodeSpaceRanges
                && _characterCodeToUnicode.Count == 0
                && _codeToCid.Count == 0
                && IsRangeBucketsEmpty(_unicodeRangesByLength)
                && IsRangeBucketsEmpty(_cidRangesByLength);
        }
    }

    /// <summary>
    /// Defined CMap name.
    /// </summary>
    public PdfString? Name { get; internal set; }

    /// <summary>
    /// System info for CID mappings, if applicable.
    /// </summary>
    public PdfCidSystemInfo? CidSystemInfo { get; internal set; }

    /// <summary>
    /// Writing mode (/WMode) for this CMap (horizontal/vertical).
    /// </summary>
    public CMapWritingMode WritingMode { get; internal set; }

    /// <summary>
    /// A read-only view of the explicit character-code-to-CID mapping entries stored in this CMap.
    /// </summary>
    public ReadOnlyDictionary<PdfCharacterCode, int> CodeToCid => new(_codeToCid);

    /// <summary>
    /// A read-only view of the explicit character-code-to-Unicode mapping entries stored in this CMap.
    /// </summary>
    public ReadOnlyDictionary<PdfCharacterCode, string> CodeToUnicode => new(_characterCodeToUnicode);

    /// <summary>
    /// Adds a codespace range pair. Start and end must have the same length (1..4 bytes).
    /// Values are interpreted as big-endian.
    /// </summary>
    public void AddCodespaceRange(in PdfCharacterCode start, in PdfCharacterCode end)
    {
        if (start.Length == 0 || end.Length == 0 || start.Length != end.Length || start.Length > 4)
        {
            return;
        }

        uint vStart = start.Value;
        uint vEnd = end.Value;

        if (vEnd < vStart)
        {
            _codeSpaceRanges.Add(new CodeSpaceRange(start.Length, vEnd, vStart));
        }
        else
        {
            _codeSpaceRanges.Add(new CodeSpaceRange(start.Length, vStart, vEnd));
        }
    }

    /// <summary>
    /// Returns the longest matching codespace length for the provided input prefix.
    /// Returns 0 if no length matches any declared range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetMaxMatchingLength(in ReadOnlySpan<byte> input)
    {
        if (_codeSpaceRanges.Count == 0 || input.Length == 0)
        {
            return 0;
        }

        int longestMatch = 0;
        for (int i = 0; i < _codeSpaceRanges.Count; i++)
        {
            CodeSpaceRange range = _codeSpaceRanges[i];
            if (input.Length < range.Length)
            {
                continue;
            }

            if (range.Length > longestMatch && MatchesEveryBytePosition(input.Slice(0, range.Length), range))
            {
                longestMatch = range.Length;
            }
        }

        return longestMatch;
    }

    // A codespace range's Start/End bound each byte position independently (as UTF-8's own byte
    // ranges do), so a candidate code only matches when every one of its bytes falls between the
    // corresponding bytes of Start and End - not merely when the whole multi-byte value, read as a
    // single big-endian integer, falls between the packed Start and End values. The latter would
    // wrongly accept a byte sequence like E0 E0 E0 against the 3-byte UTF-8 range E080800-EFBFBF:
    // numerically 0xE0E0E0 sits inside [0xE08080, 0xEFBFBF], even though its second byte, E0, is
    // not a valid UTF-8 continuation byte (which the range restricts to 80-BF at that position).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesEveryBytePosition(in ReadOnlySpan<byte> candidate, in CodeSpaceRange range)
    {
        for (int position = 0; position < range.Length; position++)
        {
            int shift = 8 * (range.Length - 1 - position);
            var lowByte = (byte)(range.Start >> shift);
            var highByte = (byte)(range.End >> shift);

            if (candidate[position] < lowByte || candidate[position] > highByte)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds a single mapping for a length-aware code (byte sequence) to Unicode.
    /// </summary>
    public void AddMapping(in PdfCharacterCode code, string unicode)
    {
        if (unicode == null)
        {
            return;
        }

        _characterCodeToUnicode[code] = unicode;
    }

    /// <summary>
    /// Adds a single mapping for a length-aware code (byte sequence) to CID.
    /// </summary>
    public void AddCidMapping(in PdfCharacterCode code, int cid) => _codeToCid[code] = cid;

    /// <summary>
    /// Adds a sequential range mapping using length-aware start/end codes to Unicode.
    /// Both start and end must be the same length (1..4 bytes), inclusive.
    /// Stored as a compressed range for efficient lookup and memory usage.
    /// </summary>
    public void AddRangeMapping(in PdfCharacterCode startCode, in PdfCharacterCode endCode, int startUnicode)
    {
        if (startCode.Length == 0 || endCode.Length == 0 || startCode.Length != endCode.Length || startCode.Length > 4)
        {
            return;
        }

        int len = startCode.Length;
        uint vStart = startCode.Value;
        uint vEnd = endCode.Value;
        if (vEnd < vStart)
        {
            uint temp = vStart;
            vStart = vEnd;
            vEnd = temp;
        }

        List<UnicodeRangeMap>? list = _unicodeRangesByLength[len];
        PdfCMapUtilities.InsertUnicodeRangeSorted(list, new UnicodeRangeMap(len, vStart, vEnd, startUnicode));
    }

    /// <summary>
    /// Adds a sequential range mapping using length-aware start/end codes to CIDs.
    /// Both start and end must be the same length (1..4 bytes), inclusive.
    /// Stored as a compressed range for efficient lookup and memory usage.
    /// </summary>
    public void AddCidRangeMapping(in PdfCharacterCode startCode, in PdfCharacterCode endCode, int startCid)
    {
        if (startCode.Length == 0 || endCode.Length == 0 || startCode.Length != endCode.Length || startCode.Length > 4)
        {
            return;
        }

        int len = startCode.Length;
        uint vStart = startCode.Value;
        uint vEnd = endCode.Value;
        if (vEnd < vStart)
        {
            uint temp = vStart;
            vStart = vEnd;
            vEnd = temp;
        }

        List<CidRangeMap>? list = _cidRangesByLength[len];
        PdfCMapUtilities.InsertCidRangeSorted(list, new CidRangeMap(len, vStart, vEnd, startCid));
    }

    /// <summary>
    /// Looks up the Unicode mapping for a length-aware code.
    /// </summary>
    public string? GetUnicode(in PdfCharacterCode code)
    {
        if (_characterCodeToUnicode.TryGetValue(code, out string? unicode))
        {
            return unicode;
        }

        int len = code.Length;
        if (len < 1 || len > 4)
        {
            return null;
        }

        uint value = code.Value;
        List<UnicodeRangeMap>? ranges = _unicodeRangesByLength[len];
        if (ranges == null || ranges.Count == 0)
        {
            return null;
        }

        int index = PdfCMapUtilities.BinarySearchUnicode(ranges, value);
        if (index >= 0)
        {
            UnicodeRangeMap r = ranges[index];
            var delta = (int)(value - r.Start);
            int codePoint = r.StartUnicode + delta;
            if (IsValidCodePoint(codePoint))
            {
                return char.ConvertFromUtf32(codePoint);
            }
        }

        return null;
    }

    /// <summary>
    /// Looks up the CID mapping for a length-aware code.
    /// </summary>
    public bool TryGetCid(in PdfCharacterCode code, out int cid)
    {
        if (_codeToCid.TryGetValue(code, out cid))
        {
            return true;
        }

        int len = code.Length;
        if (len < 1 || len > 4)
        {
            cid = 0;
            return false;
        }

        uint value = code.Value;
        List<CidRangeMap>? ranges = _cidRangesByLength[len];
        if (ranges != null && ranges.Count > 0)
        {
            int index = PdfCMapUtilities.BinarySearchCid(ranges, value);
            if (index >= 0)
            {
                CidRangeMap r = ranges[index];
                var delta = (int)(value - r.Start);
                cid = r.StartCid + delta;
                return true;
            }
        }

        if (TryConvertUnicodeToCid(code, out cid))
        {
            return true;
        }

        cid = 0;
        return false;
    }

    /// <summary>
    /// Converts a unicode string entry from bfchar/bfrange to an integer CID.
    /// Some encoding CMaps store CID values as byte strings in bfchar/bfrange sections
    /// instead of cidchar/cidrange. The bytes are interpreted as a big-endian integer.
    /// </summary>
    private bool TryConvertUnicodeToCid(in PdfCharacterCode code, out int cid)
    {
        if (_characterCodeToUnicode.TryGetValue(code, out string? unicode) && unicode != null)
        {
            cid = ConvertCidString(unicode);
            return cid >= 0;
        }

        cid = 0;
        return false;
    }

    private static int ConvertCidString(string cidString)
    {
        switch (cidString.Length)
        {
            case 1:
                return cidString[0];

            case 2:
                return (cidString[0] << 8) | cidString[1];

            default:
                return -1;
        }
    }

    /// <summary>
    /// Merges mappings from another CMap. Keys are treated the same (PdfCharacterCode).
    /// Codespace ranges are merged and Min/Max recalculated.
    /// Also merges range-based maps per code length (1..4).
    /// </summary>
    public void MergeFrom(PdfCMap other, bool overwriteExisting = false)
    {
        if (other == null)
        {
            return;
        }

        foreach (KeyValuePair<PdfCharacterCode, string> kvp in other._characterCodeToUnicode)
        {
            if (overwriteExisting || !_characterCodeToUnicode.ContainsKey(kvp.Key))
            {
                _characterCodeToUnicode[kvp.Key] = kvp.Value;
            }
        }

        foreach (KeyValuePair<PdfCharacterCode, int> kvp in other._codeToCid)
        {
            if (overwriteExisting || !_codeToCid.ContainsKey(kvp.Key))
            {
                _codeToCid[kvp.Key] = kvp.Value;
            }
        }

        _codeSpaceRanges.AddRange(other._codeSpaceRanges);

        for (int len = LengthBuckets<object>.MinLength; len <= LengthBuckets<object>.MaxLength; len++)
        {
            List<UnicodeRangeMap>? otherUnicodeRanges = other._unicodeRangesByLength[len];
            if (otherUnicodeRanges != null)
            {
                for (int i = 0; i < otherUnicodeRanges.Count; i++)
                {
                    PdfCMapUtilities.InsertUnicodeRangeSorted(_unicodeRangesByLength[len], otherUnicodeRanges[i]);
                }
            }

            List<CidRangeMap>? otherCidRanges = other._cidRangesByLength[len];
            if (otherCidRanges != null)
            {
                for (int i = 0; i < otherCidRanges.Count; i++)
                {
                    PdfCMapUtilities.InsertCidRangeSorted(_cidRangesByLength[len], otherCidRanges[i]);
                }
            }
        }
    }

    /// <summary>
    /// Returns the CID range mappings stored for the given code byte length (1..4), or <see langword="null"/> if none.
    /// </summary>
    public List<CidRangeMap>? GetCidRanges(int codeLength) => _cidRangesByLength[codeLength];

    /// <summary>
    /// Returns the Unicode range mappings stored for the given code byte length (1..4), or <see langword="null"/> if none.
    /// </summary>
    public List<UnicodeRangeMap>? GetUnicodeRanges(int codeLength) => _unicodeRangesByLength[codeLength];

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="codePoint"/> is a valid Unicode scalar value:
    /// in the range [0, 0x10FFFF] and not in the surrogate range [0xD800, 0xDFFF].
    /// </summary>
    public static bool IsValidCodePoint(int codePoint) => codePoint >= 0 && codePoint <= 0x10FFFF && (codePoint < 0xD800 || codePoint > 0xDFFF);

    /// <summary>
    /// The CMap held by <paramref name="key"/> of <paramref name="dictionary"/>: the predefined CMap it names,
    /// or the embedded CMap stream it points at. Returns <see langword="null"/> when absent or not parsable.
    /// </summary>
    /// <param name="dictionary">Dictionary holding the CMap entry.</param>
    /// <param name="key">Key of the CMap entry.</param>
    /// <param name="useCMapDepth">The number of /UseCMap entries followed to reach this entry.</param>
    internal static PdfCMap? GetCMap(PdfDictionary dictionary, in PdfString key, int useCMapDepth = 0)
    {
        PdfCMap? embeddedCMap = dictionary.Document.ObjectCache.GetOrCreate(dictionary, key, cmapObject => FromObject(cmapObject, useCMapDepth), requiresStream: true);
        if (embeddedCMap != null)
        {
            return embeddedCMap;
        }

        PdfString? predefinedName = dictionary.GetName(key);
        if (predefinedName == null)
        {
            return null;
        }

        return dictionary.Document.CMapCache.GetCmap(predefinedName.Value);
    }

    /// <summary>
    /// Parses a CMap stream object into a <see cref="PdfCMap"/>, applying the CMap entries of its stream
    /// dictionary on top of the stream body.
    /// Returns <see langword="null"/> when the stream carries no data or does not parse.
    /// </summary>
    /// <param name="cmapObject">The CMap stream object to parse.</param>
    /// <param name="useCMapDepth">The number of /UseCMap entries followed to reach this stream.</param>
    private static PdfCMap? FromObject(PdfObject cmapObject, int useCMapDepth)
    {
        ReadOnlyMemory<byte> data = cmapObject.Stream.DecodeAsMemory();
        if (data.IsEmpty)
        {
            return null;
        }

        PdfCMap result = PdfCMapScanner.Scan(data, cmapObject.Document.CMapCache.GetCmap);

        result.ApplyCMapStreamDictionary(cmapObject.Dictionary, useCMapDepth);

        return result;
    }

    /// <summary>
    /// Applies the /CMapName, /CIDSystemInfo, /WMode and /UseCMap entries of a CMap stream dictionary to this
    /// CMap parsed from the stream body. /CMapName replaces the parsed name; /CIDSystemInfo and /WMode fill in
    /// what the body left at its default; /UseCMap contributes the mappings and codespace ranges of the CMap it
    /// designates, which the body's own entries take precedence over.
    /// </summary>
    /// <param name="cmapDictionary">The stream dictionary of the CMap being loaded.</param>
    /// <param name="useCMapDepth">The number of /UseCMap entries followed to reach this dictionary.</param>
    private void ApplyCMapStreamDictionary(PdfDictionary cmapDictionary, int useCMapDepth)
    {
        if (useCMapDepth < MaxUseCMapDepth)
        {
            PdfCMap? baseCMap = GetCMap(cmapDictionary, PdfTokens.UseCMapKey, useCMapDepth + 1);
            if (baseCMap != null)
            {
                MergeFrom(baseCMap);
            }
        }

        PdfString? cmapName = cmapDictionary.GetName(PdfTokens.CMapNameKey);
        if (cmapName != null)
        {
            Name = cmapName;
        }

        PdfDictionary? cidSystemInfo = cmapDictionary.GetDictionary(PdfTokens.CidSystemInfoKey);
        if (CidSystemInfo == null && cidSystemInfo != null)
        {
            CidSystemInfo = new PdfCidSystemInfo(cidSystemInfo);
        }

        if (WritingMode == CMapWritingMode.Horizontal)
        {
            int? writingMode = cmapDictionary.GetInteger(PdfTokens.WModeKey);
            if (writingMode != null)
            {
                WritingMode = (CMapWritingMode)writingMode.Value;
            }
        }
    }

    private static bool IsRangeBucketsEmpty<T>(LengthBuckets<T> buckets)
    {
        for (int length = LengthBuckets<T>.MinLength; length <= LengthBuckets<T>.MaxLength; length++)
        {
            List<T>? bucket = buckets[length];
            if (bucket != null && bucket.Count > 0)
            {
                return false;
            }
        }

        return true;
    }
}
