using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Reads a CFF charset: the per-GID SID (name-keyed fonts) or CID (CID-keyed fonts) assignment,
/// either from an explicit table (format 0, 1, or 2) or one of the three predefined charsets
/// (ISOAdobe, Expert, ExpertSubset).
/// </summary>
public class CffCharsetReader
{
    private readonly ILogger<CffCharsetReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffCharsetReader"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public CffCharsetReader(ILogger<CffCharsetReader> logger) => _logger = logger;

    /// <summary>
    /// Reads a CFF charset.
    /// </summary>
    /// <param name="cffData">The complete CFF data.</param>
    /// <param name="charsetOffset">The charset offset from the Top DICT, or a predefined charset ID
    /// (0 = ISOAdobe, 1 = Expert, 2 = ExpertSubset).</param>
    /// <param name="glyphCount">The number of glyphs (CharStrings INDEX count) this charset must cover.</param>
    /// <returns>The parsed charset, or null if the data is malformed.</returns>
    public CffCharset? Read(in ReadOnlySpan<byte> cffData, int charsetOffset, int glyphCount)
    {
        if (glyphCount <= 0)
        {
            _logger.LogWarning("Cannot read CFF charset for a font with no glyphs.");
            return null;
        }

        if (charsetOffset is CffConstants.CharsetPredefinedIsoAdobe or CffConstants.CharsetPredefinedExpert or CffConstants.CharsetPredefinedExpertSubset)
        {
            return ReadPredefined(charsetOffset, glyphCount);
        }

        return ReadExplicit(cffData, charsetOffset, glyphCount);
    }

    private CffCharset? ReadExplicit(in ReadOnlySpan<byte> cffData, int charsetOffset, int glyphCount)
    {
        if (charsetOffset < 0 || charsetOffset >= cffData.Length)
        {
            _logger.LogWarning("Invalid CFF charset offset {Offset}.", charsetOffset);
            return null;
        }

        CffBinaryReader reader = new(cffData.Slice(charsetOffset));
        if (!reader.TryReadByte(out byte format))
        {
            _logger.LogWarning("Failed to read CFF charset: missing format byte.");
            return null;
        }

        var sidsByGid = new ushort[glyphCount];

        bool success = format switch
        {
            0 => ReadFormat0(ref reader, sidsByGid),
            1 => ReadFormat1(ref reader, sidsByGid),
            2 => ReadFormat2(ref reader, sidsByGid),
            _ => LogUnsupportedFormat(format)
        };

        if (!success)
        {
            return null;
        }

        return new CffCharset { Format = format, SidsByGid = sidsByGid };
    }

    private static bool ReadFormat0(ref CffBinaryReader reader, ushort[] sidsByGid)
    {
        for (int gid = 1; gid < sidsByGid.Length; gid++)
        {
            if (!reader.TryReadUInt16BE(out sidsByGid[gid]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ReadFormat1(ref CffBinaryReader reader, ushort[] sidsByGid)
    {
        int gid = 1;
        while (gid < sidsByGid.Length)
        {
            if (!reader.TryReadUInt16BE(out ushort rangeFirstSid) || !reader.TryReadByte(out byte rangeLeftCount))
            {
                return false;
            }

            int rangeCount = rangeLeftCount + 1;
            for (int i = 0; i < rangeCount && gid < sidsByGid.Length; i++)
            {
                sidsByGid[gid++] = (ushort)(rangeFirstSid + i);
            }
        }

        return true;
    }

    private static bool ReadFormat2(ref CffBinaryReader reader, ushort[] sidsByGid)
    {
        int gid = 1;
        while (gid < sidsByGid.Length)
        {
            if (!reader.TryReadUInt16BE(out ushort rangeFirstSid) || !reader.TryReadUInt16BE(out ushort rangeLeftCount))
            {
                return false;
            }

            int rangeCount = rangeLeftCount + 1;
            for (int i = 0; i < rangeCount && gid < sidsByGid.Length; i++)
            {
                sidsByGid[gid++] = (ushort)(rangeFirstSid + i);
            }
        }

        return true;
    }

    private CffCharset? ReadPredefined(int charsetId, int glyphCount)
    {
        PdfFontString[]? charsetNames = charsetId switch
        {
            CffConstants.CharsetPredefinedIsoAdobe => CffStandardStrings.IsoAdobeStrings,
            CffConstants.CharsetPredefinedExpert => CffStandardStrings.ExpertStrings,
            CffConstants.CharsetPredefinedExpertSubset => CffStandardStrings.ExpertSubsetStrings,
            _ => null
        };

        if (charsetNames == null)
        {
            _logger.LogWarning("Unknown predefined CFF charset ID {CharsetId}.", charsetId);
            return null;
        }

        var sidsByGid = new ushort[glyphCount];

        int gid = 1;
        for (int nameIndex = 0; nameIndex < charsetNames.Length && gid < glyphCount; nameIndex++)
        {
            if (!CffStandardStrings.StandardNameToSid.TryGetValue(charsetNames[nameIndex], out ushort sid))
            {
                _logger.LogWarning("Predefined CFF charset references a name outside the standard strings.");
                continue;
            }

            sidsByGid[gid++] = sid;
        }

        return new CffCharset { PredefinedId = charsetId, SidsByGid = sidsByGid };
    }

    private bool LogUnsupportedFormat(byte format)
    {
        _logger.LogWarning("Unsupported CFF charset format {Format}.", format);
        return false;
    }
}
