using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "maxp" table. Reading never fails: an unknown
/// version is read as 1.0, and a field the data is too short to carry falls back to a default.
/// <see cref="CreateDefault"/> covers the other case: synthesizing a "maxp" for a font that carries
/// none, which every other table needs since they are all sized by its glyph count.
/// </summary>
public class SfntMaxpProcessor
{
    private const uint Version05Fixed = 0x00005000;
    private const uint Version10Fixed = 0x00010000;

    // Allocation limits the TrueType interpreter reads before running a glyph's instructions. A
    // "maxp" that never carried them still has to permit hinting rather than forbid it, so these
    // fall back to the values a synthesized font conventionally states instead of 0.
    private const ushort DefaultMaxZones = 2; // instructions may use the twilight zone
    private const ushort DefaultMaxTwilightPoints = 16;
    private const ushort DefaultMaxStorage = 64;
    private const ushort DefaultMaxFunctionDefs = 64;
    private const ushort DefaultMaxStackElements = 512;

    private readonly ILogger<SfntMaxpProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntMaxpProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntMaxpProcessor(ILogger<SfntMaxpProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "maxp" table from its raw content bytes. Never fails: a version other than 0.5 or 1.0
    /// is read as 1.0, and once the data runs out every remaining field takes its default. A glyph
    /// count that could not be read comes back as 0, which the caller is expected to recover from
    /// another table rather than trust.
    /// </summary>
    public SfntMaxp Read(in ReadOnlyMemory<byte> data)
    {
        SfntReader reader = new(data.Span);

        uint versionFixed = reader.ReadUInt32() ?? Version10Fixed;
        bool isCffVersion = versionFixed == Version05Fixed;
        if (!isCffVersion && versionFixed != Version10Fixed)
        {
            _logger.LogWarning("Reading 'maxp' table with unknown version 0x{Version:X8}; reading it as version 1.0.", versionFixed);
        }

        SfntMaxp maxp = new()
        {
            Version = isCffVersion ? 0.5f : 1f,
            NumGlyphs = reader.ReadUInt16() ?? 0
        };

        if (isCffVersion)
        {
            return maxp;
        }

        maxp.MaxPoints = reader.ReadUInt16() ?? 0;
        maxp.MaxContours = reader.ReadUInt16() ?? 0;
        maxp.MaxCompositePoints = reader.ReadUInt16() ?? 0;
        maxp.MaxCompositeContours = reader.ReadUInt16() ?? 0;
        maxp.MaxZones = reader.ReadUInt16() ?? DefaultMaxZones;
        maxp.MaxTwilightPoints = reader.ReadUInt16() ?? DefaultMaxTwilightPoints;
        maxp.MaxStorage = reader.ReadUInt16() ?? DefaultMaxStorage;
        maxp.MaxFunctionDefs = reader.ReadUInt16() ?? DefaultMaxFunctionDefs;
        maxp.MaxInstructionDefs = reader.ReadUInt16() ?? 0;
        maxp.MaxStackElements = reader.ReadUInt16() ?? DefaultMaxStackElements;
        maxp.MaxSizeOfInstructions = reader.ReadUInt16() ?? 0;
        maxp.MaxComponentElements = reader.ReadUInt16() ?? 0;
        maxp.MaxComponentDepth = reader.ReadUInt16() ?? 0;

        if (!reader.IsValid)
        {
            _logger.LogWarning(
                "Reading truncated 'maxp' table: version 1.0 needs more than the {ActualLength} bytes present; the fields past the end took their defaults.",
                data.Length);
        }

        return maxp;
    }

    /// <summary>
    /// Creates a "maxp" model for a font that carries none, at the version its outline format
    /// implies: 0.5 for CFF outlines, 1.0 for TrueType ones.
    /// </summary>
    /// <param name="numGlyphs">The glyph count to state, recovered from whichever table the caller
    /// could size it from.</param>
    /// <param name="hasTrueTypeOutlines">Whether the font draws its glyphs from "glyf" rather than "CFF ".</param>
    public static SfntMaxp CreateDefault(ushort numGlyphs, bool hasTrueTypeOutlines)
    {
        SfntMaxp maxp = new()
        {
            Version = hasTrueTypeOutlines ? 1f : 0.5f,
            NumGlyphs = numGlyphs
        };

        if (!hasTrueTypeOutlines)
        {
            return maxp;
        }

        maxp.MaxZones = DefaultMaxZones;
        maxp.MaxTwilightPoints = DefaultMaxTwilightPoints;
        maxp.MaxStorage = DefaultMaxStorage;
        maxp.MaxFunctionDefs = DefaultMaxFunctionDefs;
        maxp.MaxStackElements = DefaultMaxStackElements;

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
