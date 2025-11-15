using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfReader.Fonts.Cff;

/// <summary>
/// Minimal CFF (Type 1C) reader utilities to get mappings for name-keyed CFF.
/// Converted to an instance class to allow structured logging via PdfDocument logger factory.
/// </summary>
internal class CffSidGidMapper
{
    // Constants (avoid magic numbers)
    private static readonly PdfString NotDefGlyphName = (PdfString)".notdef"u8;

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
    private const int OperatorPrivate = 18;               // Private DICT (size + offset) operator (unused here but must be skipped)
    private const int OperatorTopDictMax = 21;            // Highest single-byte top DICT operator code (21 = SyntheticBase)

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
    private static readonly Dictionary<PdfString, ushort> StandardNameToSid = BuildStandardNameToSid();

    private readonly ILogger<CffSidGidMapper> _logger;

    public CffSidGidMapper(PdfDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _logger = document.LoggerFactory.CreateLogger<CffSidGidMapper>();
    }

    private static Dictionary<PdfString, ushort> BuildStandardNameToSid()
    {
        var standardNameToSidMap = new Dictionary<PdfString, ushort>(CffData.StandardStrings.Length);
        for (ushort sid = 0; sid < CffData.StandardStrings.Length; sid++)
        {
            var standardName = CffData.StandardStrings[sid];
            if (!standardName.IsEmpty && !standardNameToSidMap.ContainsKey(standardName))
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
    public bool TryParseNameKeyed(ReadOnlyMemory<byte> cffDataMemory, out CffInfo info)
    {
        var cffBytes = cffDataMemory;
        info = null;

        try
        {
            var reader = new CffDataReader(cffBytes.Span);

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
                LogMultipleTopDicts(nameIndexCount, nameIndexDataStart, nameIndexOffsets, cffBytes.Span, topDictCount);
            }

            // Use first Top DICT
            var topDictStart = topDictDataStart + (topDictOffsets[0] - 1);
            var topDictEnd = topDictDataStart + (topDictOffsets[1] - 1);
            if (topDictStart < 0 || topDictEnd > cffBytes.Length || topDictEnd <= topDictStart)
            {
                return false;
            }
            var topDictBytes = cffBytes.Slice(topDictStart, topDictEnd - topDictStart);

            if (!TryParseTopDict(topDictBytes.Span, out int charsetOffset, out int charStringsOffset, out int encodingOffset, out bool isCidKeyed))
            {
                return false;
            }

            if (charStringsOffset <= 0 || charStringsOffset >= cffBytes.Length)
            {
                return false;
            }

            // CharStrings INDEX (determine glyph count)
            var charStringsReader = new CffDataReader(cffBytes.Span)
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
                if (!TryReadExplicitCharsetSids(cffBytes.Span, charsetOffset, glyphCount, out sidByGlyph))
                {
                    return false;
                }
            }

            // String INDEX (custom strings after StandardStrings)
            var stringIndexReader = new CffDataReader(cffBytes.Span)
            {
                Position = stringIndexStart
            };
            if (!TryReadIndex(ref stringIndexReader, out int stringIndexCount, out int stringIndexDataStart, out int[] stringIndexOffsets, out _))
            {
                return false;
            }
            var customStrings = new PdfString[stringIndexCount];
            for (int stringIndex = 0; stringIndex < stringIndexCount; stringIndex++)
            {
                int start = stringIndexDataStart + (stringIndexOffsets[stringIndex] - 1);
                int end = stringIndexDataStart + (stringIndexOffsets[stringIndex + 1] - 1);
                if (start < 0 || end < start || end > cffBytes.Length)
                {
                    customStrings[stringIndex] = default;
                    continue;
                }
                var slice = cffBytes.Slice(start, end - start);
                customStrings[stringIndex] = slice;
            }

            // Build name->GID & SID->GID maps
            var glyphNameToGid = new Dictionary<PdfString, ushort>(glyphCount);
            for (ushort glyphId = 0; glyphId < sidByGlyph.Length; glyphId++)
            {
                ushort sid = sidByGlyph[glyphId];

                PdfString glyphName = ResolveGlyphName(sid, customStrings);
                if (!glyphName.IsEmpty && !glyphNameToGid.ContainsKey(glyphName))
                {
                    glyphNameToGid[glyphName] = glyphId;
                }
            }

            PdfFontEncoding encoding = encodingOffset switch
            {
                PredefinedEncodingStandard => PdfFontEncoding.StandardEncoding,
                PredefinedEncodingExpert => PdfFontEncoding.MacExpertEncoding,
                _ => PdfFontEncoding.Unknown
            };

            info = new CffInfo
            {
                GlyphCount = glyphCount,
                NameToGid = glyphNameToGid,
                IsCidFont = isCidKeyed,
                GidToSid = sidByGlyph,
                Encoding = encoding,
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

    private static PdfString ResolveGlyphName(ushort sid, PdfString[] customStrings)
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

        return default;
    }

    private static PdfString[] GetCharsetNames(int charsetId)
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
                return Array.Empty<PdfString>();
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

        PdfString[] charsetGlyphNames = GetCharsetNames(charsetId);
        var seenNames = new HashSet<PdfString>();

        int glyphId = FirstRealGlyphGid;
        for (int i = 0; i < charsetGlyphNames.Length && glyphId < glyphCount; i++)
        {
            var glyphName = charsetGlyphNames[i];
            if (glyphName.IsEmpty || glyphName == NotDefGlyphName)
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

    private static bool TryGetStandardSid(PdfString name, out ushort sid)
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

            // Handle all single-byte top DICT operators (0..21). We only use 15,16,17 for mapping.
            if (opByte <= OperatorTopDictMax)
            {
                if (opByte == OperatorCharset)
                {
                    if (operandStack.Count > 0)
                    {
                        charsetOffset = (int)operandStack[operandStack.Count - 1];
                    }
                }
                else if (opByte == OperatorEncoding)
                {
                    if (operandStack.Count > 0)
                    {
                        encodingOffset = (int)operandStack[operandStack.Count - 1];
                    }
                }
                else if (opByte == OperatorCharStrings)
                {
                    if (operandStack.Count > 0)
                    {
                        charStringsOffset = (int)operandStack[operandStack.Count - 1];
                    }
                }
                // OperatorPrivate (18) and others are intentionally ignored for current mapping needs.
                operandStack.Clear();
                continue;
            }

            // Operand decoding
            if (opByte >= OperandIntLow && opByte <= OperandIntHigh)
            {
                operandStack.Add(opByte - 139);
                continue;
            }
            if (opByte >= OperandPositiveIntStart && opByte <= OperandPositiveIntEnd)
            {
                if (position >= topDictBytes.Length)
                {
                    return false;
                }
                byte nextByte = topDictBytes[position++];
                int operandInt = (opByte - 247) * 256 + nextByte + 108;
                operandStack.Add(operandInt);
                continue;
            }
            if (opByte >= OperandNegativeIntStart && opByte <= OperandNegativeIntEnd)
            {
                if (position >= topDictBytes.Length)
                {
                    return false;
                }
                byte nextByte = topDictBytes[position++];
                int operandInt = -(opByte - 251) * 256 - nextByte - 108;
                operandStack.Add(operandInt);
                continue;
            }
            if (opByte == OperandShortInt)
            {
                if (position + 1 >= topDictBytes.Length)
                {
                    return false;
                }
                short operandShort = (short)(topDictBytes[position] << 8 | topDictBytes[position + 1]);
                position += 2;
                operandStack.Add(operandShort);
                continue;
            }
            if (opByte == OperandLongInt)
            {
                if (position + 3 >= topDictBytes.Length)
                {
                    return false;
                }
                int operandInt = topDictBytes[position] << 24 | topDictBytes[position + 1] << 16 | topDictBytes[position + 2] << 8 | topDictBytes[position + 3];
                position += 4;
                operandStack.Add(operandInt);
                continue;
            }
            if (opByte == OperandRealNumber)
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
                continue;
            }

            // Unknown byte pattern -> treat as failure (malformed DICT)
            return false;
        }

        // Success if we obtained a CharStrings offset (charset may be predefined 0/1/2)
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
