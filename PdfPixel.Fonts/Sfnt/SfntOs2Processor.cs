using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "OS/2" table (versions 0 through 5).
/// </summary>
public class SfntOs2Processor
{
    private const int BaseLength = 78;
    private const int Version1Length = BaseLength + 8;
    private const int Version2Length = Version1Length + 10;
    private const int Version5Length = Version2Length + 4;
    private const int PanoseLength = 10;

    private readonly ILogger<SfntOs2Processor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntOs2Processor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntOs2Processor(ILogger<SfntOs2Processor> logger) => _logger = logger;

    /// <summary>
    /// Parses an "OS/2" table from its raw content bytes. Returns null if it is shorter than the
    /// fixed 78-byte version 0 layout, or if its version claims fields that don't fit.
    /// </summary>
    public SfntOs2? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < BaseLength)
        {
            _logger.LogWarning("Failed to read 'OS/2' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", BaseLength, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);
        SfntOs2 os2 = new()
        {
            Version = reader.ReadUInt16OrDefault(),
            XAvgCharWidth = reader.ReadInt16OrDefault(),
            UsWeightClass = reader.ReadUInt16OrDefault(),
            UsWidthClass = reader.ReadUInt16OrDefault(),
            FsType = reader.ReadUInt16OrDefault(),
            YSubscriptXSize = reader.ReadInt16OrDefault(),
            YSubscriptYSize = reader.ReadInt16OrDefault(),
            YSubscriptXOffset = reader.ReadInt16OrDefault(),
            YSubscriptYOffset = reader.ReadInt16OrDefault(),
            YSuperscriptXSize = reader.ReadInt16OrDefault(),
            YSuperscriptYSize = reader.ReadInt16OrDefault(),
            YSuperscriptXOffset = reader.ReadInt16OrDefault(),
            YSuperscriptYOffset = reader.ReadInt16OrDefault(),
            YStrikeoutSize = reader.ReadInt16OrDefault(),
            YStrikeoutPosition = reader.ReadInt16OrDefault(),
            SFamilyClass = reader.ReadInt16OrDefault(),
            Panose = reader.ReadBytes(PanoseLength).ToArray(),
            UlUnicodeRange1 = reader.ReadUInt32OrDefault(),
            UlUnicodeRange2 = reader.ReadUInt32OrDefault(),
            UlUnicodeRange3 = reader.ReadUInt32OrDefault(),
            UlUnicodeRange4 = reader.ReadUInt32OrDefault(),
            AchVendId = new SfntTableTag(reader.ReadUInt32OrDefault()),
            FsSelection = reader.ReadUInt16OrDefault(),
            UsFirstCharIndex = reader.ReadUInt16OrDefault(),
            UsLastCharIndex = reader.ReadUInt16OrDefault(),
            STypoAscender = reader.ReadInt16OrDefault(),
            STypoDescender = reader.ReadInt16OrDefault(),
            STypoLineGap = reader.ReadInt16OrDefault(),
            UsWinAscent = reader.ReadUInt16OrDefault(),
            UsWinDescent = reader.ReadUInt16OrDefault()
        };

        if (os2.Version == 0)
        {
            return os2;
        }

        if (data.Length < Version1Length)
        {
            _logger.LogWarning(
                "Failed to read 'OS/2' table: version {Version} claims code page range fields but data is only {ActualLength} bytes, expected at least {ExpectedLength}.",
                os2.Version,
                data.Length,
                Version1Length);
            return null;
        }

        os2.UlCodePageRange1 = reader.ReadUInt32OrDefault();
        os2.UlCodePageRange2 = reader.ReadUInt32OrDefault();

        if (os2.Version == 1)
        {
            return os2;
        }

        if (data.Length < Version2Length)
        {
            _logger.LogWarning(
                "Failed to read 'OS/2' table: version {Version} claims x-height/cap-height fields but data is only {ActualLength} bytes, expected at least {ExpectedLength}.",
                os2.Version,
                data.Length,
                Version2Length);
            return null;
        }

        os2.SxHeight = reader.ReadInt16OrDefault();
        os2.SCapHeight = reader.ReadInt16OrDefault();
        os2.UsDefaultChar = reader.ReadUInt16OrDefault();
        os2.UsBreakChar = reader.ReadUInt16OrDefault();
        os2.UsMaxContext = reader.ReadUInt16OrDefault();

        if (os2.Version != 5)
        {
            return os2;
        }

        if (data.Length < Version5Length)
        {
            _logger.LogWarning(
                "Failed to read 'OS/2' table: version 5 claims optical point size fields but data is only {ActualLength} bytes, expected at least {ExpectedLength}.",
                data.Length,
                Version5Length);
            return null;
        }

        os2.UsLowerOpticalPointSize = reader.ReadUInt16OrDefault();
        os2.UsUpperOpticalPointSize = reader.ReadUInt16OrDefault();

        return os2;
    }

    /// <summary>
    /// Writes an "OS/2" table's binary content, at the length its <see cref="SfntOs2.Version"/>
    /// implies. Fields introduced by a later version than the model's own are omitted; fields the
    /// version requires but that are unset on the model write as 0.
    /// </summary>
    public byte[] Write(SfntOs2 os2)
    {
        if (os2 == null)
        {
            throw new ArgumentNullException(nameof(os2));
        }

        SfntWriter writer = new();
        writer.WriteUInt16(os2.Version);
        writer.WriteInt16(os2.XAvgCharWidth);
        writer.WriteUInt16(os2.UsWeightClass);
        writer.WriteUInt16(os2.UsWidthClass);
        writer.WriteUInt16(os2.FsType);
        writer.WriteInt16(os2.YSubscriptXSize);
        writer.WriteInt16(os2.YSubscriptYSize);
        writer.WriteInt16(os2.YSubscriptXOffset);
        writer.WriteInt16(os2.YSubscriptYOffset);
        writer.WriteInt16(os2.YSuperscriptXSize);
        writer.WriteInt16(os2.YSuperscriptYSize);
        writer.WriteInt16(os2.YSuperscriptXOffset);
        writer.WriteInt16(os2.YSuperscriptYOffset);
        writer.WriteInt16(os2.YStrikeoutSize);
        writer.WriteInt16(os2.YStrikeoutPosition);
        writer.WriteInt16(os2.SFamilyClass);
        writer.WriteBytes(os2.Panose);
        writer.WriteUInt32(os2.UlUnicodeRange1);
        writer.WriteUInt32(os2.UlUnicodeRange2);
        writer.WriteUInt32(os2.UlUnicodeRange3);
        writer.WriteUInt32(os2.UlUnicodeRange4);
        writer.WriteUInt32(os2.AchVendId.Value);
        writer.WriteUInt16(os2.FsSelection);
        writer.WriteUInt16(os2.UsFirstCharIndex);
        writer.WriteUInt16(os2.UsLastCharIndex);
        writer.WriteInt16(os2.STypoAscender);
        writer.WriteInt16(os2.STypoDescender);
        writer.WriteInt16(os2.STypoLineGap);
        writer.WriteUInt16(os2.UsWinAscent);
        writer.WriteUInt16(os2.UsWinDescent);

        if (os2.Version == 0)
        {
            return writer.ToArray();
        }

        writer.WriteUInt32(os2.UlCodePageRange1.GetValueOrDefault());
        writer.WriteUInt32(os2.UlCodePageRange2.GetValueOrDefault());

        if (os2.Version == 1)
        {
            return writer.ToArray();
        }

        writer.WriteInt16(os2.SxHeight.GetValueOrDefault());
        writer.WriteInt16(os2.SCapHeight.GetValueOrDefault());
        writer.WriteUInt16(os2.UsDefaultChar.GetValueOrDefault());
        writer.WriteUInt16(os2.UsBreakChar.GetValueOrDefault());
        writer.WriteUInt16(os2.UsMaxContext.GetValueOrDefault());

        if (os2.Version != 5)
        {
            return writer.ToArray();
        }

        writer.WriteUInt16(os2.UsLowerOpticalPointSize.GetValueOrDefault());
        writer.WriteUInt16(os2.UsUpperOpticalPointSize.GetValueOrDefault());

        return writer.ToArray();
    }
}
