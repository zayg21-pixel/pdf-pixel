using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "maxp" table.
/// </summary>
public class SfntMaxpProcessor
{
    private const int Version05Length = 6;
    private const int Version10Length = 32;
    private const uint Version05Fixed = 0x00005000;
    private const uint Version10Fixed = 0x00010000;

    private readonly ILogger<SfntMaxpProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntMaxpProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntMaxpProcessor(ILogger<SfntMaxpProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "maxp" table from its raw content bytes. Returns null if it is shorter than the
    /// fixed 6-byte version 0.5 layout, or if its version claims 1.0 but is shorter than the fixed
    /// 32-byte version 1.0 layout.
    /// </summary>
    public SfntMaxp? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < Version05Length)
        {
            _logger.LogWarning("Failed to read 'maxp' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", Version05Length, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);
        uint versionFixed = reader.ReadUInt32OrDefault();
        ushort numGlyphs = reader.ReadUInt16OrDefault();

        SfntMaxp maxp = new()
        {
            Version = versionFixed / 65536f,
            NumGlyphs = numGlyphs
        };

        if (versionFixed == Version05Fixed)
        {
            return maxp;
        }

        if (data.Length < Version10Length)
        {
            _logger.LogWarning(
                "Failed to read 'maxp' table: version {Version} claims TrueType fields but data is only {ActualLength} bytes, expected at least {ExpectedLength}.",
                maxp.Version,
                data.Length,
                Version10Length);
            return null;
        }

        maxp.MaxPoints = reader.ReadUInt16OrDefault();
        maxp.MaxContours = reader.ReadUInt16OrDefault();
        maxp.MaxCompositePoints = reader.ReadUInt16OrDefault();
        maxp.MaxCompositeContours = reader.ReadUInt16OrDefault();
        maxp.MaxZones = reader.ReadUInt16OrDefault();
        maxp.MaxTwilightPoints = reader.ReadUInt16OrDefault();
        maxp.MaxStorage = reader.ReadUInt16OrDefault();
        maxp.MaxFunctionDefs = reader.ReadUInt16OrDefault();
        maxp.MaxInstructionDefs = reader.ReadUInt16OrDefault();
        maxp.MaxStackElements = reader.ReadUInt16OrDefault();
        maxp.MaxSizeOfInstructions = reader.ReadUInt16OrDefault();
        maxp.MaxComponentElements = reader.ReadUInt16OrDefault();
        maxp.MaxComponentDepth = reader.ReadUInt16OrDefault();

        return maxp;
    }

    /// <summary>
    /// Writes a "maxp" table's binary content: 6 bytes for version 0.5, or 32 bytes for version 1.0
    /// (the TrueType-only fields are expected to be set whenever <see cref="SfntMaxp.Version"/> is
    /// 1.0; a missing field writes as 0).
    /// </summary>
    public byte[] Write(SfntMaxp maxp)
    {
        if (maxp == null)
        {
            throw new ArgumentNullException(nameof(maxp));
        }

        SfntWriter writer = new();
        writer.WriteUInt32((maxp.Version == 0.5f) ? Version05Fixed : Version10Fixed);
        writer.WriteUInt16(maxp.NumGlyphs);

        if (maxp.Version == 0.5f)
        {
            return writer.ToArray();
        }

        writer.WriteUInt16(maxp.MaxPoints.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxContours.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxCompositePoints.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxCompositeContours.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxZones.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxTwilightPoints.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxStorage.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxFunctionDefs.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxInstructionDefs.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxStackElements.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxSizeOfInstructions.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxComponentElements.GetValueOrDefault());
        writer.WriteUInt16(maxp.MaxComponentDepth.GetValueOrDefault());

        return writer.ToArray();
    }
}
