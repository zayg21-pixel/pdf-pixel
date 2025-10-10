using PdfReader.Text;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using PdfReader.Models;

namespace PdfReader.Fonts.Cff
{
    /// <summary>
    /// Minimal CFF (Type 1C) reader utilities to get mappings for name-keyed CFF.
    /// Converted to an instance class to allow structured logging via PdfDocument logger factory.
    /// </summary>
    internal class CffSidGidMapper
    {
        // Constants (avoid magic numbers)
        private const string NotDefGlyphName = ".notdef";

        private const int PredefinedCharsetIsoAdobe = 0;      // ISOAdobe charset id
        private const int PredefinedCharsetExpert = 1;        // Expert charset id
        private const int PredefinedCharsetExpertSubset = 2;  // ExpertSubset charset id

        private const int PredefinedEncodingStandard = 0;     // StandardEncoding id
        private const int PredefinedEncodingExpert = 1;       // ExpertEncoding id

        // DICT operator bytes (single byte unless escape operator 12 precedes)
        private const byte OperatorEscape = 12;               // Escape marker for 2-byte operators
        private const byte OperatorEscapedRos = 30;           // 12 30 => ROS (CID-keyed) operator
        private const int OperatorCharset = 15;               // Charset offset operator
        private const int OperatorEncoding = 16;              // Encoding offset operator
        private const int OperatorCharStrings = 17;           // CharStrings offset operator

        // Number encoding boundaries (CFF DICT operand encoding spec)
        private const byte OperandIntLow = 32;                // Inclusive start of single byte int encoding
        private const byte OperandIntHigh = 246;              // Inclusive end of single byte int encoding
        private const byte OperandPositiveIntStart = 247;     // Start of two-byte positive int encoding
        private const byte OperandPositiveIntEnd = 250;       // End of two-byte positive int encoding
        private const byte OperandNegativeIntStart = 251;     // Start of two-byte negative int encoding
        private const byte OperandNegativeIntEnd = 254;       // End of two-byte negative int encoding
        private const byte OperandShortInt = 28;              // Marker for 16-bit int (big endian)
        private const byte OperandLongInt = 29;               // Marker for 32-bit int (big endian)
        private const byte OperandRealNumber = 30;            // Marker for real number (nibbles)

        private const int NotDefSid = 0;                      // SID for .notdef
        private const int FirstRealGlyphGid = 1;              // GID 0 reserved for .notdef

        // Cache for fast name->SID lookup over the StandardStrings table (O(1) instead of O(n)).
        private static readonly Dictionary<string, ushort> StandardNameToSid = BuildStandardNameToSid();

        private readonly ILogger<CffSidGidMapper> _logger;

        public CffSidGidMapper(PdfDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _logger = document.LoggerFactory.CreateLogger<CffSidGidMapper>();
        }

        private static Dictionary<string, ushort> BuildStandardNameToSid()
        {
            var standardNameToSidMap = new Dictionary<string, ushort>(CffData.StandardStrings.Length, StringComparer.Ordinal);
            for (ushort sid = 0; sid < CffData.StandardStrings.Length; sid++)
            {
                var standardName = CffData.StandardStrings[sid];
                if (!string.IsNullOrEmpty(standardName) && !standardNameToSidMap.ContainsKey(standardName))
                {
                    standardNameToSidMap[standardName] = sid;
                }
            }

            return standardNameToSidMap;
        }

        /// <summary>
        /// Attempt to parse a name-keyed (non-CID) CFF font and produce glyph mapping metadata.
        /// Returns false on any structural parse failure or if the font is CID-keyed.
        /// </summary>
        /// <param name="cffDataMemory">Raw CFF table bytes.</param>
        /// <param name="info">Resulting mapping information.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        public bool TryParseNameKeyed(ReadOnlyMemory<byte> cffDataMemory, out CffNameKeyedInfo info)
        {
            var cffBytes = cffDataMemory.Span;
            info = null;

            try
            {
                var reader = new CffDataReader(cffBytes);

                // Header
                if (!reader.TryReadByte(out _))
                {
                    return false; // major
                }
                if (!reader.TryReadByte(out _))
                {
                    return false; // minor
                }
                if (!reader.TryReadByte(out byte headerSize))
                {
                    return false;
                }
                if (!reader.TryReadByte(out _))
                {
                    return false; // offSize
                }

                // Name INDEX
                reader.Position = headerSize;
                if (!TryReadIndex(ref reader, out int nameIndexCount, out int nameIndexDataStart, out int[] nameIndexOffsets, out int topDictIndexStart))
                {
                    return false;
                }

                // Top DICT INDEX
                reader.Position = topDictIndexStart;
                if (!TryReadIndex(ref reader, out int topDictCount, out int topDictDataStart, out int[] topDictOffsets, out int stringIndexStart))
                {
                    return false;
                }
                if (topDictCount < 1)
                {
                    return false;
                }

                if (topDictCount > 1)
                {
                    LogMultipleTopDicts(nameIndexCount, nameIndexDataStart, nameIndexOffsets, cffBytes, topDictCount);
                }

                // Use first Top DICT
                var topDictStart = topDictDataStart + (topDictOffsets[0] - 1);
                var topDictEnd = topDictDataStart + (topDictOffsets[1] - 1);
                if (topDictStart < 0 || topDictEnd > cffBytes.Length || topDictEnd <= topDictStart)
                {
                    return false;
                }
                var topDictBytes = cffBytes.Slice(topDictStart, topDictEnd - topDictStart);

                if (!TryParseTopDict(topDictBytes, out int charsetOffset, out int charStringsOffset, out int encodingOffset, out bool isCidKeyed))
                {
                    return false;
                }
                if (isCidKeyed)
                {
                    return false; // only name-keyed handled here
                }
                if (charStringsOffset <= 0 || charStringsOffset >= cffBytes.Length)
                {
                    return false;
                }

                // CharStrings INDEX (determine glyph count)
                var charStringsReader = new CffDataReader(cffBytes)
                {
                    Position = charStringsOffset
                };
                if (!TryReadIndex(ref charStringsReader, out int glyphCount, out _, out int[] _, out _))
                {
                    return false;
                }
                if (glyphCount <= 0)
                {
                    return false;
                }

                // Charset -> SID list
                ushort[] sidByGlyph;
                if (charsetOffset <= PredefinedCharsetExpertSubset)
                {
                    if (!TryBuildPredefinedCharsetSids(charsetOffset, glyphCount, out sidByGlyph))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TryReadExplicitCharsetSids(cffBytes, charsetOffset, glyphCount, out sidByGlyph))
                    {
                        return false;
                    }
                }

                // String INDEX (custom strings after StandardStrings)
                var stringIndexReader = new CffDataReader(cffBytes)
                {
                    Position = stringIndexStart
                };
                if (!TryReadIndex(ref stringIndexReader, out int stringIndexCount, out int stringIndexDataStart, out int[] stringIndexOffsets, out _))
                {
                    return false;
                }
                var customStrings = new string[stringIndexCount];
                for (int stringIndex = 0; stringIndex < stringIndexCount; stringIndex++)
                {
                    int start = stringIndexDataStart + (stringIndexOffsets[stringIndex] - 1);
                    int end = stringIndexDataStart + (stringIndexOffsets[stringIndex + 1] - 1);
                    if (start < 0 || end < start || end > cffBytes.Length)
                    {
                        customStrings[stringIndex] = string.Empty;
                        continue;
                    }
                    var slice = cffBytes.Slice(start, end - start);
                    customStrings[stringIndex] = Encoding.ASCII.GetString(slice.ToArray());
                }

                // Build name->GID & SID->GID maps
                var glyphNameToGid = new Dictionary<string, ushort>(glyphCount, StringComparer.Ordinal);
                var sidToGid = new Dictionary<ushort, ushort>(glyphCount);
                for (ushort glyphId = 0; glyphId < sidByGlyph.Length; glyphId++)
                {
                    ushort sid = sidByGlyph[glyphId];
                    if (!sidToGid.ContainsKey(sid))
                    {
                        sidToGid[sid] = glyphId;
                    }

                    string glyphName = ResolveGlyphName(sid, customStrings);
                    if (!string.IsNullOrEmpty(glyphName) && !glyphNameToGid.ContainsKey(glyphName))
                    {
                        glyphNameToGid[glyphName] = glyphId;
                    }
                }

                // Build code->GID from Encoding
                var codeToGid = BuildCodeToGidMap(encodingOffset, cffBytes, glyphCount, glyphNameToGid, sidToGid);

                info = new CffNameKeyedInfo
                {
                    IsCidKeyed = isCidKeyed,
                    CharsetOffset = charsetOffset,
                    CharStringsOffset = charStringsOffset,
                    EncodingOffset = encodingOffset,
                    GlyphCount = glyphCount,
                    NameToGid = glyphNameToGid,
                    SidToGid = sidToGid,
                    CodeToGid = codeToGid,
                    CffData = cffDataMemory
                };

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse CFF name-keyed data.");
                info = null;
                return false;
            }
        }

        private void LogMultipleTopDicts(int nameIndexCount, int nameIndexDataStart, int[] nameIndexOffsets, ReadOnlySpan<byte> cffBytes, int topDictCount)
        {
            try
            {
                var topNames = new List<string>(nameIndexCount);
                for (int nameIndex = 0; nameIndex < nameIndexCount; nameIndex++)
                {
                    int start = nameIndexDataStart + (nameIndexOffsets[nameIndex] - 1);
                    int end = nameIndexDataStart + (nameIndexOffsets[nameIndex + 1] - 1);
                    if (start >= 0 && end >= start && end <= cffBytes.Length)
                    {
                        var slice = cffBytes.Slice(start, end - start);
                        topNames.Add(Encoding.ASCII.GetString(slice));
                    }
                }

                if (topNames.Count > 0)
                {
                    _logger.LogInformation("CFF contains {TopDictCount} Top DICTs (fonts): {FontNames}. Using the first one.", topDictCount, string.Join(", ", topNames));
                }
                else
                {
                    _logger.LogInformation("CFF contains {TopDictCount} Top DICTs (fonts). Using the first one.", topDictCount);
                }
            }
            catch
            {
                // Safe to ignore logging errors here.
                _logger.LogInformation("CFF contains {TopDictCount} Top DICTs (fonts). Using the first one.", topDictCount);
            }
        }

        private static string ResolveGlyphName(ushort sid, string[] customStrings)
        {
            if (sid < CffData.StandardStrings.Length)
            {
                return CffData.StandardStrings[sid];
            }

            int customIndex = sid - CffData.StandardStrings.Length;
            if ((uint)customIndex < (uint)customStrings.Length)
            {
                return customStrings[customIndex];
            }

            return null;
        }

        private Dictionary<byte, ushort> BuildCodeToGidMap(int encodingOffset, ReadOnlySpan<byte> cffBytes, int glyphCount, Dictionary<string, ushort> glyphNameToGid, Dictionary<ushort, ushort> sidToGid)
        {
            var codeToGid = new Dictionary<byte, ushort>();

            if (encodingOffset == PredefinedEncodingStandard || encodingOffset == PredefinedEncodingExpert)
            {
                var encodingNames = encodingOffset == PredefinedEncodingStandard ? CffData.StandardEncodingNames : CffData.ExpertEncodingNames;
                for (int code = 0; code < encodingNames.Length; code++)
                {
                    var encodingGlyphName = encodingNames[code];
                    if (string.IsNullOrEmpty(encodingGlyphName) || encodingGlyphName == NotDefGlyphName)
                    {
                        continue;
                    }

                    if (glyphNameToGid.TryGetValue(encodingGlyphName, out ushort mappedGid) && mappedGid != NotDefSid)
                    {
                        codeToGid[(byte)code] = mappedGid;
                        continue;
                    }

                    if (TryGetStandardSid(encodingGlyphName, out ushort standardSid) && sidToGid.TryGetValue(standardSid, out mappedGid) && mappedGid != NotDefSid)
                    {
                        codeToGid[(byte)code] = mappedGid;
                    }
                }

                return codeToGid;
            }

            if (encodingOffset <= 0 || encodingOffset >= cffBytes.Length)
            {
                return codeToGid; // No encoding or invalid offset
            }

            var encodingReader = new CffDataReader(cffBytes)
            {
                Position = encodingOffset
            };
            if (!encodingReader.TryReadByte(out byte encodingFormatRaw))
            {
                return codeToGid;
            }

            bool hasSupplement = (encodingFormatRaw & 0x80) != 0;
            byte encodingFormat = (byte)(encodingFormatRaw & 0x7F);

            switch (encodingFormat)
            {
                case 0:
                    if (!encodingReader.TryReadByte(out byte codeCount))
                    {
                        return codeToGid;
                    }
                    for (int i = 0; i < codeCount; i++)
                    {
                        if (!encodingReader.TryReadByte(out byte code))
                        {
                            return codeToGid;
                        }
                        ushort glyphId = (ushort)(i + FirstRealGlyphGid);
                        if (glyphId < glyphCount)
                        {
                            codeToGid[code] = glyphId;
                        }
                    }
                    break;
                case 1:
                    if (!encodingReader.TryReadByte(out byte rangeCount))
                    {
                        return codeToGid;
                    }
                    ushort gidCursor = FirstRealGlyphGid;
                    for (int rangeIndex = 0; rangeIndex < rangeCount && gidCursor < glyphCount; rangeIndex++)
                    {
                        if (!encodingReader.TryReadByte(out byte firstCode))
                        {
                            return codeToGid;
                        }
                        if (!encodingReader.TryReadByte(out byte leftCount))
                        {
                            return codeToGid;
                        }
                        int codesInRange = leftCount + 1;
                        for (int j = 0; j < codesInRange && gidCursor < glyphCount; j++)
                        {
                            byte code = (byte)(firstCode + j);
                            codeToGid[code] = gidCursor++;
                        }
                    }
                    break;
                default:
                    break; // Unsupported encoding format; ignore.
            }

            if (hasSupplement)
            {
                if (!encodingReader.TryReadByte(out byte supplementCount))
                {
                    return codeToGid;
                }
                for (int supplementIndex = 0; supplementIndex < supplementCount; supplementIndex++)
                {
                    if (!encodingReader.TryReadUInt16BE(out ushort supplementSid))
                    {
                        return codeToGid;
                    }
                    if (!encodingReader.TryReadByte(out byte supplementCode))
                    {
                        return codeToGid;
                    }
                    if (sidToGid.TryGetValue(supplementSid, out ushort supplementGid) && supplementGid != NotDefSid)
                    {
                        codeToGid[supplementCode] = supplementGid;
                    }
                }
            }

            return codeToGid;
        }

        private static string[] GetCharsetNames(int charsetId)
        {
            switch (charsetId)
            {
                case PredefinedCharsetIsoAdobe:
                    return CffData.IsoAdobeStrings; // ISOAdobe charset
                case PredefinedCharsetExpert:
                    return CffData.ExpertStrings; // Expert charset
                case PredefinedCharsetExpertSubset:
                    return CffData.ExpertSubsetStrings; // ExpertSubset charset
                default:
                    return Array.Empty<string>();
            }
        }

        // Charset / encoding helpers
        private static bool TryReadExplicitCharsetSids(ReadOnlySpan<byte> cffData, int charsetOffset, int glyphCount, out ushort[] sidByGlyph)
        {
            sidByGlyph = new ushort[glyphCount];
            sidByGlyph[0] = NotDefSid; // .notdef

            int nextGlyphId = FirstRealGlyphGid;
            var charsetReader = new CffDataReader(cffData)
            {
                Position = charsetOffset
            };
            if (!charsetReader.TryReadByte(out byte format))
            {
                return false;
            }

            switch (format)
            {
                case 0:
                    for (; nextGlyphId < glyphCount; nextGlyphId++)
                    {
                        if (!charsetReader.TryReadUInt16BE(out sidByGlyph[nextGlyphId]))
                        {
                            return false;
                        }
                    }
                    break;
                case 1:
                    while (nextGlyphId < glyphCount)
                    {
                        if (!charsetReader.TryReadUInt16BE(out ushort rangeFirstSid))
                        {
                            return false;
                        }
                        if (!charsetReader.TryReadByte(out byte rangeLeftCount))
                        {
                            return false;
                        }
                        int rangeCount = rangeLeftCount + 1;
                        for (int i = 0; i < rangeCount && nextGlyphId < glyphCount; i++)
                        {
                            sidByGlyph[nextGlyphId++] = (ushort)(rangeFirstSid + i);
                        }
                    }
                    break;
                case 2:
                    while (nextGlyphId < glyphCount)
                    {
                        if (!charsetReader.TryReadUInt16BE(out ushort rangeFirstSid2))
                        {
                            return false;
                        }
                        if (!charsetReader.TryReadUInt16BE(out ushort rangeLeftCount2))
                        {
                            return false;
                        }
                        int rangeCount2 = rangeLeftCount2 + 1;
                        for (int i = 0; i < rangeCount2 && nextGlyphId < glyphCount; i++)
                        {
                            sidByGlyph[nextGlyphId++] = (ushort)(rangeFirstSid2 + i);
                        }
                    }
                    break;
                default:
                    return false;
            }

            return true;
        }

        private static bool TryBuildPredefinedCharsetSids(int charsetId, int glyphCount, out ushort[] sidByGlyph)
        {
            sidByGlyph = new ushort[glyphCount];
            sidByGlyph[0] = NotDefSid; // .notdef

            string[] charsetGlyphNames = GetCharsetNames(charsetId);
            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            int glyphId = FirstRealGlyphGid;
            for (int i = 0; i < charsetGlyphNames.Length && glyphId < glyphCount; i++)
            {
                var glyphName = charsetGlyphNames[i];
                if (string.IsNullOrEmpty(glyphName) || glyphName == NotDefGlyphName)
                {
                    continue;
                }
                if (seenNames.Add(glyphName) && TryGetStandardSid(glyphName, out ushort sid))
                {
                    sidByGlyph[glyphId++] = sid;
                }
            }

            for (; glyphId < glyphCount; glyphId++)
            {
                sidByGlyph[glyphId] = NotDefSid;
            }

            return true;
        }

        private static bool TryGetStandardSid(string name, out ushort sid)
        {
            if (StandardNameToSid != null && StandardNameToSid.TryGetValue(name, out sid))
            {
                return true;
            }
            sid = NotDefSid;
            return false;
        }

        // DICT / INDEX parsing helpers
        private static bool TryParseTopDict(ReadOnlySpan<byte> topDictBytes, out int charsetOffset, out int charStringsOffset, out int encodingOffset, out bool isCidKeyed)
        {
            charsetOffset = 0;
            charStringsOffset = 0;
            encodingOffset = 0;
            isCidKeyed = false;

            var operandStack = new List<double>(capacity: 4);
            int position = 0;
            while (position < topDictBytes.Length)
            {
                byte opByte = topDictBytes[position++];
                if (opByte == OperatorEscape)
                {
                    if (position >= topDictBytes.Length)
                    {
                        break; // truncated escaped operator
                    }

                    byte escapedOperator = topDictBytes[position++];
                    if (escapedOperator == OperatorEscapedRos)
                    {
                        isCidKeyed = true;
                    }
                    operandStack.Clear();
                    continue;
                }

                if (opByte <= OperatorCharStrings)
                {
                    int op = opByte;
                    if (op == OperatorCharset)
                    {
                        if (operandStack.Count > 0)
                        {
                            charsetOffset = (int)operandStack[operandStack.Count - 1];
                        }
                    }
                    else if (op == OperatorEncoding)
                    {
                        if (operandStack.Count > 0)
                        {
                            encodingOffset = (int)operandStack[operandStack.Count - 1];
                        }
                    }
                    else if (op == OperatorCharStrings)
                    {
                        if (operandStack.Count > 0)
                        {
                            charStringsOffset = (int)operandStack[operandStack.Count - 1];
                        }
                    }
                    operandStack.Clear();
                    continue;
                }

                if (opByte >= OperandIntLow && opByte <= OperandIntHigh)
                {
                    operandStack.Add(opByte - 139);
                }
                else if (opByte >= OperandPositiveIntStart && opByte <= OperandPositiveIntEnd)
                {
                    if (position >= topDictBytes.Length)
                    {
                        return false;
                    }
                    byte nextByte = topDictBytes[position++];
                    int operandInt = (opByte - 247) * 256 + nextByte + 108;
                    operandStack.Add(operandInt);
                }
                else if (opByte >= OperandNegativeIntStart && opByte <= OperandNegativeIntEnd)
                {
                    if (position >= topDictBytes.Length)
                    {
                        return false;
                    }
                    byte nextByte = topDictBytes[position++];
                    int operandInt = -(opByte - 251) * 256 - nextByte - 108;
                    operandStack.Add(operandInt);
                }
                else if (opByte == OperandShortInt)
                {
                    if (position + 1 >= topDictBytes.Length)
                    {
                        return false;
                    }
                    short operandShort = (short)(topDictBytes[position] << 8 | topDictBytes[position + 1]);
                    position += 2;
                    operandStack.Add(operandShort);
                }
                else if (opByte == OperandLongInt)
                {
                    if (position + 3 >= topDictBytes.Length)
                    {
                        return false;
                    }
                    int operandInt = topDictBytes[position] << 24 | topDictBytes[position + 1] << 16 | topDictBytes[position + 2] << 8 | topDictBytes[position + 3];
                    position += 4;
                    operandStack.Add(operandInt);
                }
                else if (opByte == OperandRealNumber)
                {
                    bool finished = false;
                    while (!finished && position < topDictBytes.Length)
                    {
                        byte nibblePair = topDictBytes[position++];
                        if ((nibblePair & 0xF) == 0xF || ((nibblePair >> 4) & 0xF) == 0xF)
                        {
                            finished = true;
                        }
                    }
                }
                else
                {
                    return false; // Unknown byte pattern
                }
            }

            return charStringsOffset != 0;
        }

        private static bool TryReadIndex(ref CffDataReader reader, out int count, out int dataStart, out int[] offsets, out int nextAfterIndex)
        {
            count = 0;
            dataStart = 0;
            nextAfterIndex = 0;
            offsets = Array.Empty<int>();

            if (!reader.TryReadUInt16BE(out ushort entryCount))
            {
                return false;
            }
            count = entryCount;
            if (count == 0)
            {
                nextAfterIndex = reader.Position;
                return true;
            }

            if (!reader.TryReadByte(out byte offSize))
            {
                return false;
            }

            int offsetEntryCount = count + 1;
            offsets = new int[offsetEntryCount];
            for (int offsetIndex = 0; offsetIndex < offsetEntryCount; offsetIndex++)
            {
                if (!reader.TryReadOffset(offSize, out int entryOffset))
                {
                    return false;
                }
                offsets[offsetIndex] = entryOffset;
            }

            dataStart = reader.Position; // absolute
            int dataSize = offsets[offsetEntryCount - 1] - 1; // last offset - 1 gives total data size
            nextAfterIndex = dataStart + dataSize;
            reader.Position = nextAfterIndex;
            return true;
        }
    }
}
