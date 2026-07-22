using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "hhea" table.
/// </summary>
public class SfntHheaProcessor
{
    private const int TableLength = 36;

    private readonly ILogger<SfntHheaProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntHheaProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntHheaProcessor(ILogger<SfntHheaProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses an "hhea" table from its raw content bytes. Returns null if it is shorter than the
    /// fixed 36-byte "hhea" table layout.
    /// </summary>
    public SfntHhea? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < TableLength)
        {
            _logger.LogWarning("Failed to read 'hhea' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", TableLength, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);

        int versionFixed = reader.ReadInt32OrDefault();

        SfntHhea hhea = new()
        {
            Version = versionFixed / 65536f,
            Ascender = reader.ReadInt16OrDefault(),
            Descender = reader.ReadInt16OrDefault(),
            LineGap = reader.ReadInt16OrDefault(),
            AdvanceWidthMax = reader.ReadUInt16OrDefault(),
            MinLeftSideBearing = reader.ReadInt16OrDefault(),
            MinRightSideBearing = reader.ReadInt16OrDefault(),
            XMaxExtent = reader.ReadInt16OrDefault(),
            CaretSlopeRise = reader.ReadInt16OrDefault(),
            CaretSlopeRun = reader.ReadInt16OrDefault(),
            CaretOffset = reader.ReadInt16OrDefault()
        };

        reader.Skip(8); // reserved x4

        hhea.MetricDataFormat = reader.ReadInt16OrDefault();
        hhea.NumberOfHMetrics = reader.ReadUInt16OrDefault();

        return hhea;
    }

    /// <summary>
    /// Writes an "hhea" table's binary content.
    /// </summary>
    public byte[] Write(SfntHhea hhea)
    {
        if (hhea == null)
        {
            throw new ArgumentNullException(nameof(hhea));
        }

        SfntWriter writer = new();

        writer.WriteInt32((int)Math.Round(hhea.Version * 65536f));
        writer.WriteInt16(hhea.Ascender);
        writer.WriteInt16(hhea.Descender);
        writer.WriteInt16(hhea.LineGap);
        writer.WriteUInt16(hhea.AdvanceWidthMax);
        writer.WriteInt16(hhea.MinLeftSideBearing);
        writer.WriteInt16(hhea.MinRightSideBearing);
        writer.WriteInt16(hhea.XMaxExtent);
        writer.WriteInt16(hhea.CaretSlopeRise);
        writer.WriteInt16(hhea.CaretSlopeRun);
        writer.WriteInt16(hhea.CaretOffset);

        for (int reservedIndex = 0; reservedIndex < 4; reservedIndex++)
        {
            writer.WriteInt16(0);
        }

        writer.WriteInt16(hhea.MetricDataFormat);
        writer.WriteUInt16(hhea.NumberOfHMetrics);

        return writer.ToArray();
    }
}
