using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// General CMap for character mapping (length-aware, character code-based).
    /// Stores both code-to-CID and code-to-Unicode mappings for full CMap support.
    /// </summary>
    public class PdfCMap
    {
        // Length-aware mappings keyed by PdfCharacterCode (byte-sequence equality)
        private readonly Dictionary<PdfCharacterCode, string> _characterCodeToUnicode = new Dictionary<PdfCharacterCode, string>();
        private readonly Dictionary<PdfCharacterCode, int> _codeToCid = new Dictionary<PdfCharacterCode, int>();

        // Codespace ranges: define valid code byte lengths and value intervals
        private readonly List<CodeSpaceRange> _codeSpaceRanges = new List<CodeSpaceRange>();

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

            int delta = 0;
            for (uint v = vStart; v <= vEnd; v++, delta++)
            {
                int codePoint = startUnicode + delta;
                var codeBytes = PdfCharacterCode.PackUIntToBigEndian(v, len);
                _characterCodeToUnicode[new PdfCharacterCode(codeBytes)] = char.ConvertFromUtf32(codePoint);
            }
        }

        /// <summary>
        /// Add a sequential range mapping using length-aware start/end codes to CIDs.
        /// Both start and end must be the same length (1..4 bytes), inclusive.
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

            int delta = 0;
            for (uint v = vStart; v <= vEnd; v++, delta++)
            {
                int cid = startCid + delta;
                var codeBytes = PdfCharacterCode.PackUIntToBigEndian(v, len);
                _codeToCid[new PdfCharacterCode(codeBytes)] = cid;
            }
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
            return _codeToCid.TryGetValue(code, out cid);
        }

        /// <summary>
        /// Merge mappings from another CMap. Keys are treated the same (PdfCharacterCode).
        /// Codespace ranges are merged and Min/Max recalculated.
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