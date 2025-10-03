using System;
using System.Collections.Generic;

namespace PdfReader.Fonts
{
    // ToUnicode CMap for character mapping (length-aware, character code-based)
    // NOTE: Keys are byte-sequence character codes, avoiding collisions between e.g. <41> and <0041>.
    public class PdfToUnicodeCMap
    {
        // Length-aware mappings keyed by PdfCid (byte-sequence equality)
        private readonly Dictionary<PdfCharacterCode, string> _characterCodeToUnicode = new Dictionary<PdfCharacterCode, string>();
        private readonly Dictionary<PdfCharacterCode, int> _characterCodeToUnicodeCodePoint = new Dictionary<PdfCharacterCode, int>();

        // Codespace ranges: define valid code byte lengths and value intervals
        private readonly List<CodeSpaceRange> _codeSpaceRanges = new List<CodeSpaceRange>();

        /// <summary>
        /// Minimum declared character code length across codespace ranges; 0 if none declared.
        /// </summary>
        public int MinCodeLength { get; private set; }

        /// <summary>
        /// Maximum declared character code length across codespace ranges; 0 if none declared.
        /// </summary>
        public int MaxCodeLength { get; private set; }

        /// <summary>
        /// True if any codespace ranges are present.
        /// </summary>
        public bool HasCodeSpaceRanges => _codeSpaceRanges.Count > 0;

        /// <summary>
        /// Add a codespace range pair. Start and end must have the same length (1..4 bytes).
        /// Values are interpreted as big-endian.
        /// </summary>
        public void AddCodespaceRange(ReadOnlySpan<byte> start, ReadOnlySpan<byte> end)
        {
            if (start.Length == 0 || end.Length == 0 || start.Length != end.Length || start.Length > 4)
            {
                return; // ignore malformed
            }

            uint vStart = PdfCharacterCode.UnpackBigEndianToUInt(start);
            uint vEnd = PdfCharacterCode.UnpackBigEndianToUInt(end);
            if (vEnd < vStart)
            {
                // swap to normalize
                uint t = vStart; vStart = vEnd; vEnd = t;
            }
            _codeSpaceRanges.Add(new CodeSpaceRange(start.Length, vStart, vEnd));

            if (MinCodeLength == 0 || start.Length < MinCodeLength) MinCodeLength = start.Length;
            if (start.Length > MaxCodeLength) MaxCodeLength = start.Length;
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

            int maxLen = Math.Min(MaxCodeLength, input.Length);
            for (int len = maxLen; len >= MinCodeLength; len--)
            {
                uint value = PdfCharacterCode.UnpackBigEndianToUInt(input.Slice(0, len));
                for (int i = 0; i < _codeSpaceRanges.Count; i++)
                {
                    var r = _codeSpaceRanges[i];
                    if (r.Length != len) continue;
                    if (value >= r.Start && value <= r.End)
                    {
                        return len;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// Add a single mapping for a length-aware CID (byte sequence).
        /// </summary>
        public void AddMapping(PdfCharacterCode code, string unicode)
        {
            if (code == null || unicode == null)
            {
                return;
            }
            _characterCodeToUnicode[code] = unicode;
            
            // Also try to parse as code point for efficiency
            if (unicode.Length == 1)
            {
                _characterCodeToUnicodeCodePoint[code] = unicode[0];
            }
            else if (unicode.Length == 2 && char.IsSurrogatePair(unicode[0], unicode[1]))
            {
                _characterCodeToUnicodeCodePoint[code] = char.ConvertToUtf32(unicode[0], unicode[1]);
            }
        }
        
        /// <summary>
        /// Add a sequential range mapping using length-aware start/end codes.
        /// Both start and end must be the same length (1..4 bytes), inclusive.
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
            if (vEnd < vStart) { uint t = vStart; vStart = vEnd; vEnd = t; }

            int delta = 0;
            for (uint v = vStart; v <= vEnd; v++, delta++)
            {
                int codePoint = startUnicode + delta;
                var codeBytes = PdfCharacterCode.PackUIntToBigEndian(v, len);
                AddMapping(new PdfCharacterCode(codeBytes), char.ConvertFromUtf32(codePoint));
            }
        }
        
        /// <summary>
        /// Lookup by length-aware CID.
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
            if (_characterCodeToUnicodeCodePoint.TryGetValue(code, out int codePoint))
            {
                return char.ConvertFromUtf32(codePoint);
            }
            return null;
        }

        /// <summary>
        /// Merge mappings from another CMap. Keys are treated the same (PdfCid).
        /// Codespace ranges are merged and Min/Max recalculated.
        /// </summary>
        public void MergeFrom(PdfToUnicodeCMap other, bool overwriteExisting = false)
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

            foreach (var kvp in other._characterCodeToUnicodeCodePoint)
            {
                if (overwriteExisting || !_characterCodeToUnicodeCodePoint.ContainsKey(kvp.Key))
                {
                    _characterCodeToUnicodeCodePoint[kvp.Key] = kvp.Value;
                }
            }

            // merge codespace ranges
            foreach (var r in other._codeSpaceRanges)
            {
                _codeSpaceRanges.Add(r);
            }
            RecomputeMinMaxLengths();
        }

        public int MappingCount => Math.Max(_characterCodeToUnicode.Count, _characterCodeToUnicodeCodePoint.Count);

        private void RecomputeMinMaxLengths()
        {
            MinCodeLength = 0;
            MaxCodeLength = 0;
            for (int i = 0; i < _codeSpaceRanges.Count; i++)
            {
                var len = _codeSpaceRanges[i].Length;
                if (MinCodeLength == 0 || len < MinCodeLength) MinCodeLength = len;
                if (len > MaxCodeLength) MaxCodeLength = len;
            }
        }

        private readonly struct CodeSpaceRange
        {
            public CodeSpaceRange(int length, uint start, uint end)
            {
                Length = length;
                Start = start;
                End = end;
            }
            public int Length { get; }
            public uint Start { get; }
            public uint End { get; }
        }
    }
}