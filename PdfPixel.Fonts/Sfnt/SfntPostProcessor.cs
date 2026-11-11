using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.EmbeddedData;
using PdfPixel.Fonts.Encodings;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads the binary form of the SFNT "post" table. A parsed <see cref="SfntPost"/> is read-only -
/// edits to it are never written back, and a written font always states the placeholder
/// <see cref="CreateEmptyStub"/> builds.
/// </summary>
public class SfntPostProcessor
{
    private const int HeaderLength = 32;
    private const uint EmptyStubVersion = 0x00030000; // 3.0: no glyph names
    private const short EmptyStubUnderlineThickness = 50;

    private static readonly PdfFontString[] StandardMacGlyphOrder =
        FontResourceConverter.FromPdfStringBlob(FontResourceLoader.GetResource("PostGlyphOrder.bin"));

    private readonly ILogger<SfntPostProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntPostProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntPostProcessor(ILogger<SfntPostProcessor> logger) => _logger = logger;

    /// <summary>
    /// Builds a minimal placeholder "post" table: format 3.0, which by spec carries no glyph names at
    /// all. For synthesizing a brand new font that needs a structurally valid "post" table but has no
    /// real glyph name data to give it (e.g. a CFF font being wrapped in an OTTO container).
    /// </summary>
    public static byte[] CreateEmptyStub()
    {
        SfntWriter writer = new();

        writer.WriteUInt32(EmptyStubVersion);
        writer.WriteUInt32(0); // italicAngle
        writer.WriteInt16(0); // underlinePosition
        writer.WriteInt16(EmptyStubUnderlineThickness);
        writer.WriteUInt32(0); // isFixedPitch
        writer.WriteUInt32(0); // minMemType42
        writer.WriteUInt32(0); // maxMemType42
        writer.WriteUInt32(0); // minMemType1
        writer.WriteUInt32(0); // maxMemType1

        return writer.Detach();
    }

    /// <summary>
    /// Parses the fixed 32-byte header of a "post" table from its raw content bytes: italic angle, underline
    /// metrics and fixed-pitch flag, present in every format. Returns null if it is shorter than the header. The
    /// glyph names are read by <see cref="ReadNameToGid"/>.
    /// </summary>
    public SfntPost? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            _logger.LogWarning("Failed to read 'post' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", HeaderLength, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);
        reader.ReadInt32OrDefault(); // format
        int italicAngleFixed = reader.ReadInt32OrDefault();
        short underlinePosition = reader.ReadInt16OrDefault();
        short underlineThickness = reader.ReadInt16OrDefault();
        uint isFixedPitch = reader.ReadUInt32OrDefault();

        return new SfntPost
        {
            ItalicAngle = italicAngleFixed / 65536f,
            UnderlinePosition = underlinePosition,
            UnderlineThickness = underlineThickness,
            IsFixedPitch = isFixedPitch != 0
        };
    }

    /// <summary>
    /// Reads the mapping from glyph name to glyph ID of a "post" table from its raw content bytes. Only formats 1.0
    /// and 2.0 carry glyph names; every other format, a table shorter than its header and a truncated format 2.0
    /// name index give null.
    /// </summary>
    public IReadOnlyDictionary<PdfFontString, ushort>? ReadNameToGid(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            return null;
        }

        SfntReader reader = new(data.Span);
        float format = reader.ReadInt32OrDefault() / 65536f;

        if (format == 1.0f)
        {
            return ReadFormat1();
        }
        else if (format == 2.0f)
        {
            return ReadFormat2NameToGid(data.Span);
        }
        else
        {
            return null;
        }
    }

    private static Dictionary<PdfFontString, ushort> ReadFormat1()
    {
        Dictionary<PdfFontString, ushort> nameToGid = new(StandardMacGlyphOrder.Length);
        for (int glyphIndex = 0; glyphIndex < StandardMacGlyphOrder.Length; glyphIndex++)
        {
            nameToGid[StandardMacGlyphOrder[glyphIndex]] = (ushort)glyphIndex;
        }

        return nameToGid;
    }

    private Dictionary<PdfFontString, ushort>? ReadFormat2NameToGid(in ReadOnlySpan<byte> data)
    {
        SfntReader reader = new(data);
        reader.Seek(HeaderLength);
        ushort numGlyphs = reader.ReadUInt16OrDefault();

        var nameIndices = new ushort[numGlyphs];
        for (int glyphIndex = 0; glyphIndex < numGlyphs; glyphIndex++)
        {
            nameIndices[glyphIndex] = reader.ReadUInt16OrDefault();
        }

        if (!reader.IsValid)
        {
            _logger.LogWarning("Failed to read 'post' table format 2.0: glyph name index array is truncated.");
            return null;
        }

        Dictionary<PdfFontString, ushort> nameToGid = new(numGlyphs);
        for (int glyphIndex = 0; glyphIndex < numGlyphs; glyphIndex++)
        {
            ushort nameIndex = nameIndices[glyphIndex];
            nameToGid[ReadGlyphName(ref reader, nameIndex)] = (ushort)glyphIndex;
        }

        return nameToGid;
    }

    private static PdfFontString ReadGlyphName(ref SfntReader reader, ushort nameIndex)
    {
        if (nameIndex < StandardMacGlyphOrder.Length)
        {
            return StandardMacGlyphOrder[nameIndex];
        }

        byte? nameLength = reader.ReadByte();
        if (nameLength == null)
        {
            return SingleByteEncodings.UndefinedCharacter;
        }

        ReadOnlySpan<byte> nameBytes = reader.ReadBytes(nameLength.Value);
        return (reader.IsValid) ? new PdfFontString(nameBytes.ToArray()) : SingleByteEncodings.UndefinedCharacter;
    }
}
