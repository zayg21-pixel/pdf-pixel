using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "gasp" table. Unlike the other table processors,
/// <see cref="Read"/> returns null for anything malformed rather than recovering what it can: the
/// table only ever asks a rasterizer for a rendering preference, so a font that states it wrongly is
/// better left stating nothing - dropping it falls back to the rasterizer's own defaults, while a
/// half-recovered range would apply a behavior the font never asked for.
/// </summary>
public class SfntGaspProcessor
{
    private const int HeaderLength = 4;
    private const int RangeLength = 4;
    private const ushort MaxSupportedVersion = 1;
    private const ushort LastRangeMaxPpem = 0xFFFF;

    private readonly ILogger<SfntGaspProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGaspProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntGaspProcessor(ILogger<SfntGaspProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "gasp" table from its raw content bytes. Returns null if the table is unusable: a
    /// version past 1 (whose range semantics are unknown), a range array the data is too short to
    /// hold, ranges not in ascending ppem order, or a last range that stops short of 0xFFFF and so
    /// leaves the sizes above it uncovered.
    /// </summary>
    public SfntGasp? Read(in ReadOnlyMemory<byte> data)
    {
        SfntReader reader = new(data.Span);

        ushort version = reader.ReadUInt16OrDefault();
        ushort rangeCount = reader.ReadUInt16OrDefault();

        if (!reader.IsValid)
        {
            _logger.LogWarning("Failed to read 'gasp' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", HeaderLength, data.Length);
            return null;
        }

        if (version > MaxSupportedVersion)
        {
            _logger.LogWarning("Failed to read 'gasp' table: unknown version {Version}.", version);
            return null;
        }

        var ranges = new SfntGaspRange[rangeCount];
        int previousMaxPpem = -1;
        for (int rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
        {
            ushort maxPpem = reader.ReadUInt16OrDefault();
            var behavior = (SfntGaspBehavior)reader.ReadUInt16OrDefault();

            if (!reader.IsValid)
            {
                _logger.LogWarning(
                    "Failed to read 'gasp' table: {RangeCount} ranges need more than the {ActualLength} bytes present.",
                    rangeCount,
                    data.Length);
                return null;
            }

            if (maxPpem <= previousMaxPpem)
            {
                _logger.LogWarning(
                    "Failed to read 'gasp' table: range {RangeIndex} states maxPpem {MaxPpem}, which does not follow the preceding range's {PreviousMaxPpem}.",
                    rangeIndex,
                    maxPpem,
                    previousMaxPpem);
                return null;
            }

            ranges[rangeIndex] = new SfntGaspRange(maxPpem, behavior);
            previousMaxPpem = maxPpem;
        }

        if (previousMaxPpem != LastRangeMaxPpem)
        {
            _logger.LogWarning("Failed to read 'gasp' table: the last of its {RangeCount} ranges does not cover every ppem size up to 0xFFFF.", rangeCount);
            return null;
        }

        return new SfntGasp
        {
            Version = version,
            Ranges = ranges
        };
    }

    /// <summary>
    /// Writes a "gasp" table's binary content: the version, the range count, then each range's ppem
    /// limit and behavior. The ranges are written in the order <see cref="SfntGasp.Ranges"/> holds
    /// them, which the spec requires to ascend by ppem limit and to end at 0xFFFF.
    /// </summary>
    public byte[] Write(SfntGasp gasp)
    {
        if (gasp == null)
        {
            throw new ArgumentNullException(nameof(gasp));
        }

        IReadOnlyList<SfntGaspRange> ranges = gasp.Ranges;

        SfntWriter writer = new(HeaderLength + (ranges.Count * RangeLength));
        writer.WriteUInt16(gasp.Version);
        writer.WriteUInt16((ushort)ranges.Count);

        for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
        {
            writer.WriteUInt16(ranges[rangeIndex].MaxPpem);
            writer.WriteUInt16((ushort)ranges[rangeIndex].Behavior);
        }

        return writer.Detach();
    }
}
