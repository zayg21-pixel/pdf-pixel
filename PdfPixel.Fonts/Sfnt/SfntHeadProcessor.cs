using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "head" table.
/// </summary>
public class SfntHeadProcessor
{
    private const int TableLength = 54;
    private const uint MagicNumber = 0x5F0F3CF5;

    /// <summary>
    /// Byte offset of the checkSumAdjustment field within a "head" table's content, for container
    /// writers to patch once the whole font's checksum is known.
    /// </summary>
    public const int CheckSumAdjustmentOffset = 8;

    private readonly ILogger<SfntHeadProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntHeadProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntHeadProcessor(ILogger<SfntHeadProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "head" table from its raw content bytes. Returns null if it is shorter than the
    /// fixed 54-byte "head" table layout, or if its magic number does not match.
    /// </summary>
    public SfntHead? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < TableLength)
        {
            _logger.LogWarning("Failed to read 'head' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", TableLength, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);

        int versionFixed = reader.ReadInt32OrDefault();
        int fontRevisionFixed = reader.ReadInt32OrDefault();
        reader.Skip(4); // checkSumAdjustment: computed by the container writer, not font-authored data
        uint magicNumber = reader.ReadUInt32OrDefault();

        if (magicNumber != MagicNumber)
        {
            _logger.LogWarning("Failed to read 'head' table: magic number 0x{MagicNumber:X8} does not match expected 0x{ExpectedMagicNumber:X8}.", magicNumber, MagicNumber);
            return null;
        }

        return new SfntHead
        {
            Version = versionFixed / 65536f,
            FontRevision = fontRevisionFixed / 65536f,
            Flags = reader.ReadUInt16OrDefault(),
            UnitsPerEm = reader.ReadUInt16OrDefault(),
            Created = reader.ReadInt64OrDefault(),
            Modified = reader.ReadInt64OrDefault(),
            XMin = reader.ReadInt16OrDefault(),
            YMin = reader.ReadInt16OrDefault(),
            XMax = reader.ReadInt16OrDefault(),
            YMax = reader.ReadInt16OrDefault(),
            MacStyle = reader.ReadUInt16OrDefault(),
            LowestRecPpem = reader.ReadUInt16OrDefault(),
            FontDirectionHint = reader.ReadInt16OrDefault(),
            IndexToLocFormat = reader.ReadInt16OrDefault(),
            GlyphDataFormat = reader.ReadInt16OrDefault()
        };
    }

    /// <summary>
    /// Writes a "head" table's binary content. The checksumAdjustment field is always written as 0 -
    /// only the container writer knows the whole font's checksum needed to patch it.
    /// </summary>
    public byte[] Write(SfntHead head)
    {
        if (head == null)
        {
            throw new ArgumentNullException(nameof(head));
        }

        SfntWriter writer = new();

        writer.WriteInt32((int)Math.Round(head.Version * 65536f));
        writer.WriteInt32((int)Math.Round(head.FontRevision * 65536f));
        writer.WriteUInt32(0); // checkSumAdjustment, patched later by the container writer
        writer.WriteUInt32(MagicNumber);
        writer.WriteUInt16(head.Flags);
        writer.WriteUInt16(head.UnitsPerEm);
        writer.WriteInt64(head.Created);
        writer.WriteInt64(head.Modified);
        writer.WriteInt16(head.XMin);
        writer.WriteInt16(head.YMin);
        writer.WriteInt16(head.XMax);
        writer.WriteInt16(head.YMax);
        writer.WriteUInt16(head.MacStyle);
        writer.WriteUInt16(head.LowestRecPpem);
        writer.WriteInt16(head.FontDirectionHint);
        writer.WriteInt16(head.IndexToLocFormat);
        writer.WriteInt16(head.GlyphDataFormat);

        return writer.ToArray();
    }
}
