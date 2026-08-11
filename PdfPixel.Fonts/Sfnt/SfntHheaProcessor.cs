using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "hhea" table. Reading never fails: a field the data
/// is too short to carry falls back to a default. <see cref="CreateDefault"/> covers the other case:
/// synthesizing an "hhea" for a font that carries none, without which "hmtx" has nothing to size it
/// and the font loses its advance widths entirely.
/// </summary>
public class SfntHheaProcessor
{
    private const int Version10Fixed = 0x00010000;

    // A caret slope of 0/0 has no direction at all; the spec's own value for an upright caret is 1/0.
    private const short DefaultCaretSlopeRise = 1;

    private readonly ILogger<SfntHheaProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntHheaProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntHheaProcessor(ILogger<SfntHheaProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses an "hhea" table from its raw content bytes. Never fails: the table has only ever had
    /// one version, so anything else is read as 1.0, and once the data runs out every remaining field
    /// takes its default. A long-entry count that could not be read comes back as 0, which the caller
    /// is expected to recover from "hmtx"'s own length rather than trust.
    /// </summary>
    public SfntHhea Read(in ReadOnlyMemory<byte> data)
    {
        SfntReader reader = new(data.Span);

        int versionFixed = reader.ReadInt32() ?? Version10Fixed;
        if (versionFixed != Version10Fixed)
        {
            _logger.LogWarning("Reading 'hhea' table with unknown version 0x{Version:X8}; reading it as version 1.0.", versionFixed);
        }

        SfntHhea hhea = new()
        {
            Version = 1f,
            Ascender = reader.ReadInt16() ?? 0,
            Descender = reader.ReadInt16() ?? 0,
            LineGap = reader.ReadInt16() ?? 0,
            AdvanceWidthMax = reader.ReadUInt16() ?? 0,
            MinLeftSideBearing = reader.ReadInt16() ?? 0,
            MinRightSideBearing = reader.ReadInt16() ?? 0,
            XMaxExtent = reader.ReadInt16() ?? 0,
            CaretSlopeRise = reader.ReadInt16() ?? DefaultCaretSlopeRise,
            CaretSlopeRun = reader.ReadInt16() ?? 0,
            CaretOffset = reader.ReadInt16() ?? 0
        };

        reader.Skip(8); // reserved x4

        hhea.MetricDataFormat = reader.ReadInt16() ?? 0;
        hhea.NumberOfHMetrics = reader.ReadUInt16() ?? 0;

        if (!reader.IsValid)
        {
            _logger.LogWarning(
                "Reading truncated 'hhea' table: {ActualLength} bytes of the fixed 36-byte layout; the fields past the end took their defaults.",
                data.Length);
        }

        return hhea;
    }

    /// <summary>
    /// Creates an "hhea" model for a font that carries none. The vertical bounds are expected to come
    /// from "head", whose yMax/yMin describe the same extent an ascender and descender do.
    /// </summary>
    /// <param name="ascender">The ascender to state, in font units.</param>
    /// <param name="descender">The descender to state, in font units.</param>
    /// <param name="numberOfHMetrics">The long-entry count to state, recovered from "hmtx"'s length.</param>
    public static SfntHhea CreateDefault(short ascender, short descender, ushort numberOfHMetrics)
    {
        return new()
        {
            Version = 1f,
            Ascender = ascender,
            Descender = descender,
            CaretSlopeRise = DefaultCaretSlopeRise,
            NumberOfHMetrics = numberOfHMetrics
        };
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

        return writer.Detach();
    }
}
