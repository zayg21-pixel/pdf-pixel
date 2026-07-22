using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Reads a CFF FDSelect structure: the GID-to-Font-DICT-index mapping for CID-keyed fonts.
/// </summary>
public class CffFdSelectReader
{
    private const byte FormatFlat = 0;
    private const byte FormatRanges = 3;

    private readonly ILogger<CffFdSelectReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffFdSelectReader"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public CffFdSelectReader(ILogger<CffFdSelectReader> logger) => _logger = logger;

    /// <summary>
    /// Reads an FDSelect structure, producing the Font DICT index for each glyph.
    /// </summary>
    /// <param name="data">The raw FDSelect bytes.</param>
    /// <param name="glyphCount">The number of glyphs (CharStrings INDEX count) this FDSelect must cover.</param>
    /// <returns>The parsed FDSelect, or null if the data is malformed.</returns>
    public CffFdSelect? Read(in ReadOnlySpan<byte> data, int glyphCount)
    {
        CffBinaryReader reader = new(data);

        if (!reader.TryReadByte(out byte format))
        {
            _logger.LogWarning("Failed to read CFF FDSelect: missing format byte.");
            return null;
        }

        int[]? fdIndexByGid = format switch
        {
            FormatFlat => ReadFlat(ref reader, glyphCount),
            FormatRanges => ReadRanges(ref reader, glyphCount),
            _ => LogUnsupportedFormat(format)
        };

        if (fdIndexByGid == null)
        {
            return null;
        }

        return new CffFdSelect { Format = format, FdIndexByGid = fdIndexByGid };
    }

    private int[]? ReadFlat(ref CffBinaryReader reader, int glyphCount)
    {
        var result = new int[glyphCount];
        for (int gid = 0; gid < glyphCount; gid++)
        {
            if (!reader.TryReadByte(out byte fdIndex))
            {
                _logger.LogWarning("CFF FDSelect format 0 ran out of data at GID {Gid} of {GlyphCount}.", gid, glyphCount);
                return null;
            }

            result[gid] = fdIndex;
        }

        return result;
    }

    private int[]? ReadRanges(ref CffBinaryReader reader, int glyphCount)
    {
        if (!reader.TryReadUInt16BE(out ushort rangeCount))
        {
            _logger.LogWarning("Failed to read CFF FDSelect format 3: missing range count.");
            return null;
        }

        var result = new int[glyphCount];
        if (!reader.TryReadUInt16BE(out ushort firstGid))
        {
            _logger.LogWarning("Failed to read CFF FDSelect format 3: missing first GID.");
            return null;
        }

        for (int rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
        {
            if (!reader.TryReadByte(out byte fdIndex))
            {
                _logger.LogWarning("Failed to read CFF FDSelect format 3: missing Font DICT index for range {RangeIndex}.", rangeIndex);
                return null;
            }

            if (!reader.TryReadUInt16BE(out ushort nextFirstGid))
            {
                _logger.LogWarning("Failed to read CFF FDSelect format 3: missing sentinel/next GID for range {RangeIndex}.", rangeIndex);
                return null;
            }

            for (int gid = firstGid; gid < nextFirstGid && gid < glyphCount; gid++)
            {
                result[gid] = fdIndex;
            }

            firstGid = nextFirstGid;
        }

        return result;
    }

    private int[]? LogUnsupportedFormat(byte format)
    {
        _logger.LogWarning("Unsupported CFF FDSelect format {Format}.", format);
        return null;
    }
}
