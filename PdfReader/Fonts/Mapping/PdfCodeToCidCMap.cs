using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// CMap for mapping content stream character codes to numeric CIDs.
    /// Length-aware and supports codespace ranges and sequential ranges.
    /// </summary>
    public class PdfCodeToCidCMap
    {
        private readonly Dictionary<PdfCharacterCode, int> _codeToCid = new Dictionary<PdfCharacterCode, int>();
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
        /// Count of explicit code-to-CID mappings in this CMap.
        /// </summary>
        public int MappingCount => _codeToCid.Count;

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
                uint temp = vStart;
                vStart = vEnd;
                vEnd = temp;
            }

            _codeSpaceRanges.Add(new CodeSpaceRange(start.Length, vStart, vEnd));

            if (MinCodeLength == 0 || start.Length < MinCodeLength)
            {
                MinCodeLength = start.Length;
            }

            if (start.Length > MaxCodeLength)
            {
                MaxCodeLength = start.Length;
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

            int maxLen = Math.Min(MaxCodeLength, input.Length);
            for (int len = maxLen; len >= MinCodeLength; len--)
            {
                uint value = PdfCharacterCode.UnpackBigEndianToUInt(input.Slice(0, len));

                for (int i = 0; i < _codeSpaceRanges.Count; i++)
                {
                    var range = _codeSpaceRanges[i];
                    if (range.Length != len)
                    {
                        continue;
                    }

                    if (value >= range.Start && value <= range.End)
                    {
                        return len;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Add a single mapping for a length-aware code (byte sequence) to a numeric CID.
        /// </summary>
        public void AddMapping(PdfCharacterCode code, int cid)
        {
            if (code == null)
            {
                return;
            }

            _codeToCid[code] = cid;
        }

        /// <summary>
        /// Add a sequential range mapping using length-aware start/end codes.
        /// Both start and end must be the same length (1..4 bytes), inclusive.
        /// </summary>
        public void AddRangeMapping(ReadOnlySpan<byte> startCode, ReadOnlySpan<byte> endCode, int startCid)
        {
            if (startCode.Length == 0 || endCode.Length == 0 || startCode.Length != endCode.Length || startCode.Length > 4)
            {
                return;
            }

            int length = startCode.Length;
            uint vStart = PdfCharacterCode.UnpackBigEndianToUInt(startCode);
            uint vEnd = PdfCharacterCode.UnpackBigEndianToUInt(endCode);

            if (vEnd < vStart)
            {
                uint temp = vStart;
                vStart = vEnd;
                vEnd = temp;
            }

            int delta = 0;
            for (uint value = vStart; value <= vEnd; value++, delta++)
            {
                var codeBytes = PdfCharacterCode.PackUIntToBigEndian(value, length);
                AddMapping(new PdfCharacterCode(codeBytes), startCid + delta);
            }
        }

        /// <summary>
        /// Try to resolve a numeric CID for a given code (byte sequence).
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
        /// Merge mappings and codespace ranges from another code-to-CID CMap.
        /// Existing mappings are preserved.
        /// </summary>
        public void MergeFrom(PdfCodeToCidCMap other)
        {
            if (other == null)
            {
                return;
            }

            foreach (var kv in other._codeToCid)
            {
                if (!_codeToCid.ContainsKey(kv.Key))
                {
                    _codeToCid[kv.Key] = kv.Value;
                }
            }

            foreach (var range in other._codeSpaceRanges)
            {
                _codeSpaceRanges.Add(range);
            }

            RecomputeMinMaxLengths();
        }

        /// <summary>
        /// Recompute MinCodeLength and MaxCodeLength from codespace ranges.
        /// </summary>
        private void RecomputeMinMaxLengths()
        {
            MinCodeLength = 0;
            MaxCodeLength = 0;

            for (int i = 0; i < _codeSpaceRanges.Count; i++)
            {
                int length = _codeSpaceRanges[i].Length;

                if (MinCodeLength == 0 || length < MinCodeLength)
                {
                    MinCodeLength = length;
                }

                if (length > MaxCodeLength)
                {
                    MaxCodeLength = length;
                }
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
