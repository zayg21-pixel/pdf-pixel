using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "loca" table. Like "hmtx", it is not self-describing:
/// <see cref="Read"/> needs the glyph count (from "maxp") and the offset format (from "head"'s
/// indexToLocFormat) as inputs, since "loca" encodes offsets either as halved UInt16 values (format 0)
/// or literal UInt32 values (format 1).
/// </summary>
public class SfntLocaProcessor
{
    private readonly ILogger<SfntLocaProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntLocaProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntLocaProcessor(ILogger<SfntLocaProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "loca" table from its raw content bytes, resolving both offset formats to actual
    /// byte offsets. Returns null if the data is shorter than <c>numGlyphs + 1</c> entries.
    /// </summary>
    /// <param name="data">The "loca" table's raw content bytes.</param>
    /// <param name="numGlyphs">The "maxp" table's numGlyphs field.</param>
    /// <param name="indexToLocFormat">The "head" table's indexToLocFormat field: 0 for short (halved
    /// UInt16) entries, any other value for long (UInt32) entries.</param>
    public SfntLoca? Read(in ReadOnlyMemory<byte> data, ushort numGlyphs, short indexToLocFormat)
    {
        int entryCount = numGlyphs + 1;
        bool isLongFormat = indexToLocFormat != 0;
        int expectedLength = entryCount * (isLongFormat ? 4 : 2);

        if (data.Length < expectedLength)
        {
            _logger.LogWarning("Failed to read 'loca' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", expectedLength, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);
        var offsets = new uint[entryCount];
        for (int index = 0; index < entryCount; index++)
        {
            offsets[index] = isLongFormat ? reader.ReadUInt32OrDefault() : (uint)(reader.ReadUInt16OrDefault() * 2);
        }

        return new SfntLoca { Offsets = offsets };
    }

    /// <summary>
    /// Writes a "loca" table's binary content in the format <paramref name="indexToLocFormat"/>
    /// specifies, which must be the same value written to <c>head.indexToLocFormat</c>.
    /// </summary>
    public byte[] Write(SfntLoca loca, short indexToLocFormat)
    {
        if (loca == null)
        {
            throw new ArgumentNullException(nameof(loca));
        }

        SfntWriter writer = new();
        bool isLongFormat = indexToLocFormat != 0;
        IReadOnlyList<uint> offsets = loca.Offsets;
        for (int index = 0; index < offsets.Count; index++)
        {
            if (isLongFormat)
            {
                writer.WriteUInt32(offsets[index]);
            }
            else
            {
                writer.WriteUInt16((ushort)(offsets[index] / 2));
            }
        }

        return writer.ToArray();
    }
}
