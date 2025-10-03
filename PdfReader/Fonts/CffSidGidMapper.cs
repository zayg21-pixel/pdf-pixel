using System;
using System.Collections.Generic;
using System.Text;

namespace PdfReader.Fonts
{
    // --- Aggregate parse result ---
    internal sealed class CffNameKeyedInfo
    {
        public bool IsCidKeyed { get; set; }
        public int CharsetOffset { get; set; }
        public int CharStringsOffset { get; set; }
        public int EncodingOffset { get; set; }
        public int GlyphCount { get; set; }
        public Dictionary<string, ushort> NameToGid { get; set; }
        public Dictionary<ushort, ushort> SidToGid { get; set; }
        public Dictionary<byte, ushort> CodeToGid { get; set; }
        public ReadOnlyMemory<byte> CffData { get; set; }
    }

    /// <summary>
    /// Minimal CFF (Type 1C) reader utilities to get mappings for name-keyed CFF.
    /// </summary>
    internal static class CffSidGidMapper
    {
        // Cache for fast name->SID lookup over the StandardStrings table (O(1) instead of O(n)).
        private static readonly Dictionary<string, ushort> StandardNameToSid = BuildStandardNameToSid();

        private static Dictionary<string, ushort> BuildStandardNameToSid()
        {
            var map = new Dictionary<string, ushort>(CffData.StandardStrings.Length, StringComparer.Ordinal);
            for (ushort i = 0; i < CffData.StandardStrings.Length; i++)
            {
                var name = CffData.StandardStrings[i];
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                {
                    map[name] = i;
                }
            }
            return map;
        }

        // --- Public APIs ---

        public static bool TryParseNameKeyed(ReadOnlyMemory<byte> cffDataMemory, out CffNameKeyedInfo info)
        {
            var cffBytes = cffDataMemory.Span;
            info = null;
            try
            {
                var reader = new Reader(cffBytes);

                // Header
                if (!reader.TryReadByte(out _)) return false; // major
                if (!reader.TryReadByte(out _)) return false; // minor
                if (!reader.TryReadByte(out byte headerSize)) return false;
                if (!reader.TryReadByte(out _)) return false; // offSize

                // Name INDEX
                reader.Position = headerSize;
                if (!TryReadIndex(ref reader, out int nameIndexCount, out int nameIndexDataStart, out int[] nameIndexOffsets, out int topDictIndexStart)) return false;

                // Top DICT INDEX
                reader.Position = topDictIndexStart;
                if (!TryReadIndex(ref reader, out int topDictCount, out int topDictDataStart, out var topDictOffsets, out int stringIndexStart)) return false;
                if (topDictCount < 1) return false;
                if (topDictCount > 1)
                {
                    try
                    {
                        var topNames = new List<string>(nameIndexCount);
                        for (int nameIdx = 0; nameIdx < nameIndexCount; nameIdx++)
                        {
                            int start = nameIndexDataStart + (nameIndexOffsets[nameIdx] - 1);
                            int end = nameIndexDataStart + (nameIndexOffsets[nameIdx + 1] - 1);
                            if (start >= 0 && end >= start && end <= cffBytes.Length)
                            {
                                var slice = cffBytes.Slice(start, end - start);
                                topNames.Add(Encoding.ASCII.GetString(slice));
                            }
                        }
                        if (topNames.Count > 0)
                        {
                            Console.WriteLine($"CFF contains {topDictCount} Top DICTs (fonts): {string.Join(", ", topNames)}. Using the first one.");
                        }
                        else
                        {
                            Console.WriteLine($"CFF contains {topDictCount} Top DICTs (fonts). Using the first one.");
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"CFF contains {topDictCount} Top DICTs (fonts). Using the first one.");
                    }
                }

                // Use first Top DICT
                var topDictStart = topDictDataStart + (topDictOffsets[0] - 1);
                var topDictEnd = topDictDataStart + (topDictOffsets[1] - 1);
                if (topDictStart < 0 || topDictEnd > cffBytes.Length || topDictEnd <= topDictStart) return false;
                var topDictBytes = cffBytes.Slice(topDictStart, topDictEnd - topDictStart);

                if (!TryParseTopDict(topDictBytes, out int charsetOffset, out int charStringsOffset, out int encodingOffset, out bool isCidKeyed)) return false;
                if (isCidKeyed) return false; // only name-keyed here
                if (charStringsOffset <= 0 || charStringsOffset >= cffBytes.Length) return false;

                // CharStrings INDEX -> glyph count
                var charStringsReader = new Reader(cffBytes) { Position = charStringsOffset };
                if (!TryReadIndex(ref charStringsReader, out int glyphCount, out _, out int[] _, out _)) return false;
                if (glyphCount <= 0) return false;

                // charset -> SID list
                ushort[] sids;
                if (charsetOffset <= 2)
                {
                    if (!TryBuildPredefinedCharsetSids(charsetOffset, glyphCount, out sids)) return false;
                }
                else
                {
                    if (!TryReadExplicitCharsetSids(cffBytes, charsetOffset, glyphCount, out sids)) return false;
                }

                // Read String INDEX for custom strings
                var stringIndexReader = new Reader(cffBytes) { Position = stringIndexStart };
                if (!TryReadIndex(ref stringIndexReader, out int stringIndexCount, out int stringIndexDataStart, out int[] stringIndexOffsets, out _)) return false;
                var customStrings = new string[stringIndexCount];
                for (int i = 0; i < stringIndexCount; i++)
                {
                    int start = stringIndexDataStart + (stringIndexOffsets[i] - 1);
                    int end = stringIndexDataStart + (stringIndexOffsets[i + 1] - 1);
                    if (start < 0 || end < start || end > cffBytes.Length) { customStrings[i] = string.Empty; continue; }
                    var slice = cffBytes.Slice(start, end - start);
                    customStrings[i] = Encoding.ASCII.GetString(slice.ToArray());
                }

                // Build name->gid and sid->gid strictly from charset-referenced SIDs
                var nameToGid = new Dictionary<string, ushort>(glyphCount, StringComparer.Ordinal);
                var sidToGid = new Dictionary<ushort, ushort>(glyphCount);
                for (ushort gid = 0; gid < sids.Length; gid++)
                {
                    ushort sid = sids[gid];
                    if (!sidToGid.ContainsKey(sid)) sidToGid[sid] = gid;

                    string glyphName = null;
                    if (sid < CffData.StandardStrings.Length)
                    {
                        glyphName = CffData.StandardStrings[sid];
                    }
                    else
                    {
                        int idx = sid - CffData.StandardStrings.Length;
                        if ((uint)idx < (uint)customStrings.Length)
                        {
                            glyphName = customStrings[idx];
                        }
                    }

                    if (!string.IsNullOrEmpty(glyphName) && !nameToGid.ContainsKey(glyphName))
                    {
                        nameToGid[glyphName] = gid;
                    }
                }

                // Build code->gid from Encoding
                var codeToGid = new Dictionary<byte, ushort>();
                if (encodingOffset == 0 || encodingOffset == 1)
                {
                    // 0 = StandardEncoding, 1 = ExpertEncoding
                    var encodingNames = encodingOffset == 0 ? CffData.StandardEncodingNames : CffData.ExpertEncodingNames;
                    for (int code = 0; code < encodingNames.Length; code++)
                    {
                        var encName = encodingNames[code];
                        if (string.IsNullOrEmpty(encName) || encName == ".notdef")
                        {
                            continue;
                        }
                        if (nameToGid.TryGetValue(encName, out ushort encGid) && encGid != 0) { codeToGid[(byte)code] = encGid; continue; }
                        if (TryGetStandardSid(encName, out ushort encSid) && sidToGid.TryGetValue(encSid, out encGid) && encGid != 0) codeToGid[(byte)code] = encGid;
                    }
                }
                else if (encodingOffset > 0 && encodingOffset < cffBytes.Length)
                {
                    var encodingReader = new Reader(cffBytes) { Position = encodingOffset };
                    if (!encodingReader.TryReadByte(out byte encodingFormatRaw)) return false;
                    bool hasSupplement = (encodingFormatRaw & 0x80) != 0;
                    byte encodingFormat = (byte)(encodingFormatRaw & 0x7F);

                    switch (encodingFormat)
                    {
                        case 0:
                            if (!encodingReader.TryReadByte(out byte nCodes)) return false;
                            for (int i = 0; i < nCodes; i++)
                            {
                                if (!encodingReader.TryReadByte(out byte code)) return false;
                                ushort gid = (ushort)(i + 1);
                                if (gid < glyphCount) codeToGid[code] = gid;
                            }
                            break;
                        case 1:
                            if (!encodingReader.TryReadByte(out byte nRanges)) return false;
                            ushort gidCursor = 1;
                            for (int rIndex = 0; rIndex < nRanges && gidCursor < glyphCount; rIndex++)
                            {
                                if (!encodingReader.TryReadByte(out byte firstCode)) return false;
                                if (!encodingReader.TryReadByte(out byte nLeftByte)) return false;
                                int rangeCount = nLeftByte + 1;
                                for (int j = 0; j < rangeCount && gidCursor < glyphCount; j++)
                                {
                                    byte code = (byte)(firstCode + j);
                                    codeToGid[code] = gidCursor++;
                                }
                            }
                            break;
                        default:
                            break;
                    }

                    if (hasSupplement)
                    {
                        if (!encodingReader.TryReadByte(out byte supplementCount)) return false;
                        for (int i = 0; i < supplementCount; i++)
                        {
                            if (!encodingReader.TryReadUInt16BE(out ushort supplementSid)) return false;
                            if (!encodingReader.TryReadByte(out byte supplementCode)) return false;
                            if (sidToGid.TryGetValue(supplementSid, out ushort gid) && gid != 0) codeToGid[supplementCode] = gid;
                        }
                    }
                }

                info = new CffNameKeyedInfo
                {
                    IsCidKeyed = isCidKeyed,
                    CharsetOffset = charsetOffset,
                    CharStringsOffset = charStringsOffset,
                    EncodingOffset = encodingOffset,
                    GlyphCount = glyphCount,
                    NameToGid = nameToGid,
                    SidToGid = sidToGid,
                    CodeToGid = codeToGid,
                    CffData = cffDataMemory
                };
                return true;
            }
            catch
            {
                info = null;
                return false;
            }
        }

        private static string[] GetCharsetNames(int charsetId)
        {
            switch (charsetId)
            {
                case 0:
                    return CffData.IsoAdobeStrings;     // ISOAdobe charset
                case 1:
                    return CffData.ExpertStrings;       // Expert charset
                case 2:
                    return CffData.ExpertSubsetStrings; // ExpertSubset charset
                default:
                    return Array.Empty<string>();
            }
        }

        // --- Helpers: charset/encoding ---

        private static bool TryReadExplicitCharsetSids(ReadOnlySpan<byte> cffData, int charsetOffset, int glyphCount, out ushort[] sids)
        {
            sids = new ushort[glyphCount];
            sids[0] = 0; // .notdef
            int nextGid = 1;
            var charsetReader = new Reader(cffData) { Position = charsetOffset };
            if (!charsetReader.TryReadByte(out byte format)) return false;
            switch (format)
            {
                case 0:
                    for (; nextGid < glyphCount; nextGid++)
                    {
                        if (!charsetReader.TryReadUInt16BE(out sids[nextGid])) return false;
                    }
                    break;
                case 1:
                    while (nextGid < glyphCount)
                    {
                        if (!charsetReader.TryReadUInt16BE(out ushort rangeFirstSid)) return false;
                        if (!charsetReader.TryReadByte(out byte rangeLeftCountByte)) return false;
                        int rangeCount = rangeLeftCountByte + 1;
                        for (int i = 0; i < rangeCount && nextGid < glyphCount; i++)
                        {
                            sids[nextGid++] = (ushort)(rangeFirstSid + i);
                        }
                    }
                    break;
                case 2:
                    while (nextGid < glyphCount)
                    {
                        if (!charsetReader.TryReadUInt16BE(out ushort rangeFirstSid2)) return false;
                        if (!charsetReader.TryReadUInt16BE(out ushort rangeLeftCount2)) return false;
                        int rangeCount2 = rangeLeftCount2 + 1;
                        for (int i = 0; i < rangeCount2 && nextGid < glyphCount; i++)
                        {
                            sids[nextGid++] = (ushort)(rangeFirstSid2 + i);
                        }
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }

        private static bool TryBuildPredefinedCharsetSids(int charsetId, int glyphCount, out ushort[] sids)
        {
            sids = new ushort[glyphCount];
            sids[0] = 0; // .notdef

            string[] names = GetCharsetNames(charsetId);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            int gid = 1;
            for (int i = 0; i < names.Length && gid < glyphCount; i++)
            {
                var name = names[i];
                if (string.IsNullOrEmpty(name) || name == ".notdef") continue;
                if (seen.Add(name) && TryGetStandardSid(name, out ushort sid))
                {
                    sids[gid++] = sid;
                }
            }

            // If we couldn't fill, pad remaining with 0 (won't map to real glyphs)
            for (; gid < glyphCount; gid++) sids[gid] = 0;
            return true;
        }

        private static bool TryGetStandardSid(string name, out ushort sid)
        {
            if (StandardNameToSid != null && StandardNameToSid.TryGetValue(name, out sid))
            {
                return true;
            }
            sid = 0;
            return false;
        }

        // --- DICT/INDEX parsing helpers ---

        private static bool TryParseTopDict(ReadOnlySpan<byte> topDictBytes, out int charsetOffset, out int charStringsOffset, out int encodingOffset, out bool isCidKeyed)
        {
            charsetOffset = 0;
            charStringsOffset = 0;
            encodingOffset = 0;
            isCidKeyed = false;

            var operandStack = new List<double>(4);
            int position = 0;
            while (position < topDictBytes.Length)
            {
                byte opByte = topDictBytes[position++];
                if (opByte == 12)
                {
                    if (position >= topDictBytes.Length) break; // escaped operator
                    byte escapedOperator = topDictBytes[position++];
                    if (escapedOperator == 30)
                    {
                        // ROS operator => CID-keyed CFF
                        isCidKeyed = true;
                    }
                    operandStack.Clear();
                    continue;
                }

                if (opByte <= 21)
                {
                    int op = opByte;
                    if (op == 15) // charset
                    {
                        if (operandStack.Count > 0) charsetOffset = (int)operandStack[operandStack.Count - 1];
                    }
                    else if (op == 16) // Encoding
                    {
                        if (operandStack.Count > 0) encodingOffset = (int)operandStack[operandStack.Count - 1];
                    }
                    else if (op == 17) // CharStrings
                    {
                        if (operandStack.Count > 0) charStringsOffset = (int)operandStack[operandStack.Count - 1];
                    }
                    operandStack.Clear();
                    continue;
                }

                // Operand decoding
                if (opByte >= 32 && opByte <= 246)
                {
                    operandStack.Add(opByte - 139);
                }
                else if (opByte >= 247 && opByte <= 250)
                {
                    if (position >= topDictBytes.Length) return false;
                    byte nextByte = topDictBytes[position++];
                    int operandInt = (opByte - 247) * 256 + nextByte + 108;
                    operandStack.Add(operandInt);
                }
                else if (opByte >= 251 && opByte <= 254)
                {
                    if (position >= topDictBytes.Length) return false;
                    byte nextByte = topDictBytes[position++];
                    int operandInt = -(opByte - 251) * 256 - nextByte - 108;
                    operandStack.Add(operandInt);
                }
                else if (opByte == 28)
                {
                    if (position + 1 >= topDictBytes.Length) return false;
                    short operandShort = (short)((topDictBytes[position] << 8) | topDictBytes[position + 1]);
                    position += 2;
                    operandStack.Add(operandShort);
                }
                else if (opByte == 29)
                {
                    if (position + 3 >= topDictBytes.Length) return false;
                    int operandInt = (topDictBytes[position] << 24) | (topDictBytes[position + 1] << 16) | (topDictBytes[position + 2] << 8) | topDictBytes[position + 3];
                    position += 4;
                    operandStack.Add(operandInt);
                }
                else if (opByte == 30)
                {
                    bool done = false;
                    while (!done && position < topDictBytes.Length)
                    {
                        byte nibble = topDictBytes[position++];
                        if ((nibble & 0xF) == 0xF || ((nibble >> 4) & 0xF) == 0xF)
                        {
                            done = true;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }

            return charStringsOffset != 0; // charsetOffset can be 0/1/2 (predefined)
        }

        private static bool TryReadIndex(ref Reader r, out int count, out int dataStart, out int[] offsets, out int nextAfterIndex)
        {
            count = 0;
            dataStart = 0;
            nextAfterIndex = 0;
            offsets = Array.Empty<int>();

            if (!r.TryReadUInt16BE(out ushort cnt)) return false;
            count = cnt;
            if (count == 0)
            {
                nextAfterIndex = r.Position;
                return true;
            }

            if (!r.TryReadByte(out byte offSize)) return false;
            int offCount = count + 1;
            offsets = new int[offCount];
            for (int i = 0; i < offCount; i++)
            {
                if (!r.TryReadOffset(offSize, out int off)) return false;
                offsets[i] = off;
            }
            dataStart = r.Position; // absolute
            int dataSize = offsets[offCount - 1] - 1; // last offset - 1 is size
            nextAfterIndex = dataStart + dataSize;
            r.Position = nextAfterIndex;
            return true;
        }

        // --- Reader struct ---
        private ref struct Reader
        {
            private readonly ReadOnlySpan<byte> _data;
            public int Position;

            public Reader(ReadOnlySpan<byte> data)
            {
                _data = data;
                Position = 0;
            }

            public bool TryReadByte(out byte value)
            {
                if (Position >= _data.Length) { value = 0; return false; }
                value = _data[Position++];
                return true;
            }

            public bool TryReadUInt16BE(out ushort value)
            {
                if (Position + 1 >= _data.Length) { value = 0; return false; }
                value = (ushort)((_data[Position] << 8) | _data[Position + 1]);
                Position += 2;
                return true;
            }

            public bool TryReadOffset(int offSize, out int value)
            {
                value = 0;
                if (offSize < 1 || offSize > 4) return false;
                if (Position + offSize > _data.Length) return false;
                for (int i = 0; i < offSize; i++)
                {
                    value = (value << 8) | _data[Position + i];
                }
                Position += offSize;
                return true;
            }
        }
    }
}
