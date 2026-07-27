using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Reads a CFF Encoding: the character-code-to-GID assignment for a name-keyed font, either from an
/// explicit table (format 0 or 1, plus an optional supplement) or one of the two predefined encodings
/// (Standard, Expert). Only meaningful for name-keyed fonts -- CID-keyed fonts select glyphs by CID
/// via the charset, not by character code.
/// </summary>
public class CffEncodingReader
{
    private readonly ILogger<CffEncodingReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffEncodingReader"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public CffEncodingReader(ILogger<CffEncodingReader> logger) => _logger = logger;

    /// <summary>
    /// Reads a CFF Encoding.
    /// </summary>
    /// <param name="cffData">The complete CFF data.</param>
    /// <param name="encodingOffset">The encoding offset from the Top DICT, or a predefined encoding ID
    /// (0 = Standard, 1 = Expert).</param>
    /// <param name="charset">This font's already-parsed charset, used to resolve SIDs (predefined
    /// encoding names, supplement entries) back to a GID.</param>
    /// <returns>The parsed encoding, or null if the data is malformed.</returns>
    public CffEncoding? Read(in ReadOnlySpan<byte> cffData, int encodingOffset, CffCharset charset)
    {
        if (charset == null)
        {
            throw new ArgumentNullException(nameof(charset));
        }

        if (encodingOffset is CffConstants.EncodingPredefinedStandard or CffConstants.EncodingPredefinedExpert)
        {
            return ReadPredefined(encodingOffset, charset);
        }

        return ReadExplicit(cffData, encodingOffset, charset);
    }

    private CffEncoding? ReadPredefined(int predefinedId, CffCharset charset)
    {
        PdfFontEncoding baseEncoding = (predefinedId == CffConstants.EncodingPredefinedStandard)
            ? PdfFontEncoding.StandardEncoding
            : PdfFontEncoding.MacExpertEncoding;

        PdfFontString[]? codeToName = SingleByteEncodings.GetEncodingSet(baseEncoding);
        if (codeToName == null)
        {
            _logger.LogWarning("Unknown predefined CFF encoding ID {PredefinedId}.", predefinedId);
            return null;
        }

        Dictionary<ushort, ushort> sidToGid = BuildSidToGid(charset);
        var gidByCode = new ushort[256];
        for (int code = 0; code < codeToName.Length && code < gidByCode.Length; code++)
        {
            PdfFontString name = codeToName[code];
            if (name.IsEmpty || !CffStandardStrings.StandardNameToSid.TryGetValue(name, out ushort sid))
            {
                continue;
            }

            if (sidToGid.TryGetValue(sid, out ushort gid))
            {
                gidByCode[code] = gid;
            }
        }

        return new CffEncoding { PredefinedId = predefinedId, GidByCode = gidByCode };
    }

    private CffEncoding? ReadExplicit(in ReadOnlySpan<byte> cffData, int encodingOffset, CffCharset charset)
    {
        if (encodingOffset < 0 || encodingOffset >= cffData.Length)
        {
            _logger.LogWarning("Invalid CFF encoding offset {Offset}.", encodingOffset);
            return null;
        }

        CffBinaryReader reader = new(cffData.Slice(encodingOffset));
        if (!reader.TryReadByte(out byte formatByte))
        {
            _logger.LogWarning("Failed to read CFF encoding: missing format byte.");
            return null;
        }

        var format = (byte)(formatByte & CffConstants.EncodingFormatMask);
        bool hasSupplement = (formatByte & CffConstants.EncodingSupplementFlag) != 0;

        var gidByCode = new ushort[256];
        List<byte> mainTableCodes = [];
        bool success = format switch
        {
            0 => ReadFormat0(ref reader, gidByCode, mainTableCodes, charset.SidsByGid.Length),
            1 => ReadFormat1(ref reader, gidByCode, mainTableCodes, charset.SidsByGid.Length),
            _ => LogUnsupportedFormat(format)
        };

        if (!success)
        {
            return null;
        }

        CffEncodingSupplement[]? supplements = null;
        if (hasSupplement)
        {
            supplements = ReadSupplement(ref reader, gidByCode, charset);
            if (supplements == null)
            {
                return null;
            }
        }

        return new CffEncoding
        {
            Format = format,
            MainTableCodes = mainTableCodes.ToArray(),
            Supplements = supplements,
            GidByCode = gidByCode
        };
    }

    private static bool ReadFormat0(ref CffBinaryReader reader, ushort[] gidByCode, List<byte> mainTableCodes, int glyphCount)
    {
        if (!reader.TryReadByte(out byte codeCount))
        {
            return false;
        }

        for (int gid = 1; gid <= codeCount; gid++)
        {
            if (!reader.TryReadByte(out byte code))
            {
                return false;
            }

            mainTableCodes.Add(code);
            if (gid < glyphCount)
            {
                gidByCode[code] = (ushort)gid;
            }
        }

        return true;
    }

    private static bool ReadFormat1(ref CffBinaryReader reader, ushort[] gidByCode, List<byte> mainTableCodes, int glyphCount)
    {
        if (!reader.TryReadByte(out byte rangeCount))
        {
            return false;
        }

        int gid = 1;
        for (int rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
        {
            if (!reader.TryReadByte(out byte firstCode) || !reader.TryReadByte(out byte leftCount))
            {
                return false;
            }

            int rangeSize = leftCount + 1;
            for (int codeOffset = 0; codeOffset < rangeSize; codeOffset++)
            {
                int code = firstCode + codeOffset;
                if (code < gidByCode.Length)
                {
                    mainTableCodes.Add((byte)code);
                    if (gid < glyphCount)
                    {
                        gidByCode[code] = (ushort)gid;
                    }
                }

                gid++;
            }
        }

        return true;
    }

    private CffEncodingSupplement[]? ReadSupplement(ref CffBinaryReader reader, ushort[] gidByCode, CffCharset charset)
    {
        if (!reader.TryReadByte(out byte supplementCount))
        {
            _logger.LogWarning("Failed to read CFF encoding supplement: missing count byte.");
            return null;
        }

        Dictionary<ushort, ushort> sidToGid = BuildSidToGid(charset);
        var supplements = new CffEncodingSupplement[supplementCount];
        for (int supplementIndex = 0; supplementIndex < supplementCount; supplementIndex++)
        {
            if (!reader.TryReadByte(out byte code) || !reader.TryReadUInt16BE(out ushort sid))
            {
                _logger.LogWarning("CFF encoding supplement entry {Index} ran past the end of the data.", supplementIndex);
                return null;
            }

            supplements[supplementIndex] = new CffEncodingSupplement(code, sid);
            if (sidToGid.TryGetValue(sid, out ushort gid))
            {
                gidByCode[code] = gid;
            }
        }

        return supplements;
    }

    private static Dictionary<ushort, ushort> BuildSidToGid(CffCharset charset)
    {
        ushort[] sidsByGid = charset.SidsByGid;
        Dictionary<ushort, ushort> sidToGid = new(sidsByGid.Length);
        for (ushort gid = 0; gid < sidsByGid.Length; gid++)
        {
            if (!sidToGid.ContainsKey(sidsByGid[gid]))
            {
                sidToGid[sidsByGid[gid]] = gid;
            }
        }

        return sidToGid;
    }

    private bool LogUnsupportedFormat(byte format)
    {
        _logger.LogWarning("Unsupported CFF encoding format {Format}.", format);
        return false;
    }
}
