using PdfReader.Fonts.Model;
using PdfReader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PdfReader.Fonts.Mapping;

public enum CMapWMode
{
    Horizontal = 0,
    Vertical = 1
}

/// <summary>
/// General CMap for character mapping (length-aware, character code-based).
/// Stores both code-to-CID and code-to-Unicode mappings for full CMap support.
/// </summary>
public class PdfCMap
{
    private readonly Dictionary<PdfCharacterCode, string> _characterCodeToUnicode = new Dictionary<PdfCharacterCode, string>();
    private readonly Dictionary<PdfCharacterCode, int> _codeToCid = new Dictionary<PdfCharacterCode, int>();

    private readonly LengthBuckets<UnicodeRangeMap> _unicodeRangesByLength = new LengthBuckets<UnicodeRangeMap>();
    private readonly LengthBuckets<CidRangeMap> _cidRangesByLength = new LengthBuckets<CidRangeMap>();

    private readonly List<CodeSpaceRange> _codeSpaceRanges = new List<CodeSpaceRange>();

    /// <summary>
    /// True if any codespace ranges are present.
    /// </summary>
    public bool HasCodeSpaceRanges => _codeSpaceRanges.Count > 0;

    /// <summary>
    /// Defined CMap name.
    /// </summary>
    public PdfString Name { get; internal set; }

    /// <summary>
    /// System info for CID mappings, if applicable.
    /// </summary>
    public PdfCidSystemInfo CidSystemInfo { get; internal set; }

    /// <summary>
    /// WMode for this CMap (horizontal/vertical).
    /// </summary>
    public CMapWMode WMode { get; internal set; }

    /// <summary>
    /// Add a codespace range pair. Start and end must have the same length (1..4 bytes).
    /// Values are interpreted as big-endian.
    /// </summary>
    public void AddCodespaceRange(ReadOnlySpan<byte> start, ReadOnlySpan<byte> end)
    {
        if (start.Length == 0 || end.Length == 0 || start.Length != end.Length || start.Length > 4)
        {
            return;
        }

        uint vStart = PdfCharacterCode.UnpackBigEndianToUInt(start);
        uint vEnd = PdfCharacterCode.UnpackBigEndianToUInt(end);

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
    /// Return the longest matching codespace length for the provided input prefix.
    /// Returns 0 if no length matches any declared range.
    /// </summary>
    public int GetMaxMatchingLength(ReadOnlySpan<byte> input)
    {
        if (_codeSpaceRanges.Count == 0 || input.Length == 0)
        {
            return 0;
        }

        int longestMatch = 0;
        for (int i = 0; i < _codeSpaceRanges.Count; i++)
        {
            var range = _codeSpaceRanges[i];
            if (input.Length < range.Length)
            {
                continue;
            }
            uint value = PdfCharacterCode.UnpackBigEndianToUInt(input.Slice(0, range.Length));
            if (value >= range.Start && value <= range.End)
            {
                if (range.Length > longestMatch)
                {
                    longestMatch = range.Length;
                }
            }
        }
        return longestMatch;
    }

    /// <summary>
    /// Add a single mapping for a length-aware code (byte sequence) to Unicode.
    /// </summary>
    public void AddMapping(PdfCharacterCode code, string unicode)
    {
        if (code == null || unicode == null)
        {
            return;
        }
        _characterCodeToUnicode[code] = unicode;
    }

    /// <summary>
    /// Add a single mapping for a length-aware code (byte sequence) to CID.
    /// </summary>
    public void AddCidMapping(PdfCharacterCode code, int cid)
    {
        if (code == null)
        {
            return;
        }
        _codeToCid[code] = cid;
    }

    /// <summary>
    /// Add a sequential range mapping using length-aware start/end codes to Unicode.
    /// Both start and end must be the same length (1..4 bytes), inclusive.
    /// Stored as a compressed range for efficient lookup and memory usage.
    /// </summary>
    public void AddRangeMapping(ReadOnlySpan<byte> startCode, ReadOnlySpan<byte> endCode, int startUnicode)
    {
        if (startCode.Length == 0 || endCode.Length == 0 || startCode.Length != endCode.Length || startCode.Length > 4)
        {
            return;
        }
        int len = startCode.Length;
        uint vStart = PdfCharacterCode.UnpackBigEndianToUInt(startCode);
        uint vEnd = PdfCharacterCode.UnpackBigEndianToUInt(endCode);
        if (vEnd < vStart)
        {
            uint temp = vStart;
            vStart = vEnd;
            vEnd = temp;
        }

        var list = _unicodeRangesByLength[len];
        PdfCMapUtilities.InsertUnicodeRangeSorted(list, new UnicodeRangeMap(len, vStart, vEnd, startUnicode));
    }

    /// <summary>
    /// Add a sequential range mapping using length-aware start/end codes to CIDs.
    /// Both start and end must be the same length (1..4 bytes), inclusive.
    /// Stored as a compressed range for efficient lookup and memory usage.
    /// </summary>
    public void AddCidRangeMapping(ReadOnlySpan<byte> startCode, ReadOnlySpan<byte> endCode, int startCid)
    {
        if (startCode.Length == 0 || endCode.Length == 0 || startCode.Length != endCode.Length || startCode.Length > 4)
        {
            return;
        }
        int len = startCode.Length;
        uint vStart = PdfCharacterCode.UnpackBigEndianToUInt(startCode);
        uint vEnd = PdfCharacterCode.UnpackBigEndianToUInt(endCode);
        if (vEnd < vStart)
        {
            uint temp = vStart;
            vStart = vEnd;
            vEnd = temp;
        }

        var list = _cidRangesByLength[len];
        PdfCMapUtilities.InsertCidRangeSorted(list, new CidRangeMap(len, vStart, vEnd, startCid));
    }

    /// <summary>
    /// Lookup by length-aware code for Unicode mapping.
    /// </summary>
    public string GetUnicode(PdfCharacterCode code)
    {
        if (code == null)
        {
            return null;
        }
        if (_characterCodeToUnicode.TryGetValue(code, out string unicode))
        {
            return unicode;
        }

        ReadOnlySpan<byte> bytes = code.Bytes.Span;
        int len = bytes.Length;
        if (len < 1 || len > 4)
        {
            return null;
        }

        uint value = PdfCharacterCode.UnpackBigEndianToUInt(bytes);
        var ranges = _unicodeRangesByLength[len];
        if (ranges == null || ranges.Count == 0)
        {
            return null;
        }

        int index = PdfCMapUtilities.BinarySearchUnicode(ranges, value);
        if (index >= 0)
        {
            var r = ranges[index];
            int delta = (int)(value - r.Start);
            int codePoint = r.StartUnicode + delta;
            if (IsValidCodePoint(codePoint))
            {
                return char.ConvertFromUtf32(codePoint);
            }
        }

        return null;
    }

    /// <summary>
    /// Lookup by length-aware code for CID mapping.
    /// </summary>
    public bool TryGetCid(PdfCharacterCode code, out int cid)
    {
        if (code == null)
        {
            cid = 0;
            return false;
        }
        if (_codeToCid.TryGetValue(code, out cid))
        {
            return true;
        }

        ReadOnlySpan<byte> bytes = code.Bytes.Span;
        int len = bytes.Length;
        if (len < 1 || len > 4)
        {
            cid = 0;
            return false;
        }

        uint value = PdfCharacterCode.UnpackBigEndianToUInt(bytes);
        var ranges = _cidRangesByLength[len];
        if (ranges == null || ranges.Count == 0)
        {
            cid = 0;
            return false;
        }

        int index = PdfCMapUtilities.BinarySearchCid(ranges, value);
        if (index >= 0)
        {
            var r = ranges[index];
            int delta = (int)(value - r.Start);
            cid = r.StartCid + delta;
            return true;
        }

        cid = 0;
        return false;
    }

    /// <summary>
    /// Merge mappings from another CMap. Keys are treated the same (PdfCharacterCode).
    /// Codespace ranges are merged and Min/Max recalculated.
    /// Also merges range-based maps per code length (1..4).
    /// </summary>
    public void MergeFrom(PdfCMap other, bool overwriteExisting = false)
    {
        if (other == null)
        {
            return;
        }

        foreach (var kvp in other._characterCodeToUnicode)
        {
            if (overwriteExisting || !_characterCodeToUnicode.ContainsKey(kvp.Key))
            {
                _characterCodeToUnicode[kvp.Key] = kvp.Value;
            }
        }
        foreach (var kvp in other._codeToCid)
        {
            if (overwriteExisting || !_codeToCid.ContainsKey(kvp.Key))
            {
                _codeToCid[kvp.Key] = kvp.Value;
            }
        }
        foreach (var range in other._codeSpaceRanges)
        {
            _codeSpaceRanges.Add(range);
        }

        for (int len = LengthBuckets<object>.MinLength; len <= LengthBuckets<object>.MaxLength; len++)
        {
            var otherUnicodeRanges = other._unicodeRangesByLength[len];
            if (otherUnicodeRanges != null)
            {
                for (int i = 0; i < otherUnicodeRanges.Count; i++)
                {
                    PdfCMapUtilities.InsertUnicodeRangeSorted(_unicodeRangesByLength[len], otherUnicodeRanges[i]);
                }
            }

            var otherCidRanges = other._cidRangesByLength[len];
            if (otherCidRanges != null)
            {
                for (int i = 0; i < otherCidRanges.Count; i++)
                {
                    PdfCMapUtilities.InsertCidRangeSorted(_cidRangesByLength[len], otherCidRanges[i]);
                }
            }
        }
    }

    public ReadOnlyDictionary<PdfCharacterCode, int> GetCodeToCid() => new ReadOnlyDictionary<PdfCharacterCode, int>(_codeToCid);

    public IReadOnlyList<CodeSpaceRange> GetCodeSpaceRanges() => _codeSpaceRanges.ToArray();


    public static bool IsValidCodePoint(int codePoint)
    {
        return codePoint >= 0 && codePoint <= 0x10FFFF && (codePoint < 0xD800 || codePoint > 0xDFFF);
    }
}