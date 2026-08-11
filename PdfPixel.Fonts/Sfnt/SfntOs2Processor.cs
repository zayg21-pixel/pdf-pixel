using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "OS/2" table (versions 0 through 5). Reading never
/// fails: a field the data is too short to carry falls back to the value the spec treats as
/// "unspecified" for it, so a truncated table still yields a complete, writable model.
/// <see cref="CreateEmptyStub"/> covers the other case: synthesizing a placeholder "OS/2" for a font
/// that carries none at all.
/// </summary>
public class SfntOs2Processor
{
    private const int PanoseLength = 10;
    private const ushort MaxSupportedVersion = 5;

    private const ushort DefaultWeightClass = 400; // Normal
    private const ushort DefaultWidthClass = 5; // Medium
    private const ushort DefaultFsSelection = 0x0040; // REGULAR
    private const uint DefaultVendorId = 0x20202020; // four spaces: no registered vendor
    private const ushort DefaultLastCharIndex = 0xFFFF;
    private const ushort DefaultBreakChar = 32; // space
    private const ushort DefaultUpperOpticalPointSize = 0xFFFF;

    private readonly ILogger<SfntOs2Processor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntOs2Processor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntOs2Processor(ILogger<SfntOs2Processor> logger) => _logger = logger;

    /// <summary>
    /// Parses an "OS/2" table from its raw content bytes. Never fails: once the data runs out every
    /// remaining field takes the value the spec treats as "unspecified" for it, and a version past
    /// the newest one this reads is treated as that newest version, since the fields it adds are
    /// unknown here either way.
    /// </summary>
    public SfntOs2 Read(in ReadOnlyMemory<byte> data)
    {
        SfntReader reader = new(data.Span);

        ushort version = reader.ReadUInt16() ?? 0;
        if (version > MaxSupportedVersion)
        {
            _logger.LogWarning(
                "Reading 'OS/2' table with unsupported version {Version}; reading it as version {SupportedVersion}.",
                version,
                MaxSupportedVersion);
            version = MaxSupportedVersion;
        }

        SfntOs2 os2 = new()
        {
            Version = version,
            XAvgCharWidth = reader.ReadInt16() ?? 0,
            UsWeightClass = reader.ReadUInt16() ?? DefaultWeightClass,
            UsWidthClass = reader.ReadUInt16() ?? DefaultWidthClass,
            FsType = reader.ReadUInt16() ?? 0, // installable embedding
            YSubscriptXSize = reader.ReadInt16() ?? 0,
            YSubscriptYSize = reader.ReadInt16() ?? 0,
            YSubscriptXOffset = reader.ReadInt16() ?? 0,
            YSubscriptYOffset = reader.ReadInt16() ?? 0,
            YSuperscriptXSize = reader.ReadInt16() ?? 0,
            YSuperscriptYSize = reader.ReadInt16() ?? 0,
            YSuperscriptXOffset = reader.ReadInt16() ?? 0,
            YSuperscriptYOffset = reader.ReadInt16() ?? 0,
            YStrikeoutSize = reader.ReadInt16() ?? 0,
            YStrikeoutPosition = reader.ReadInt16() ?? 0,
            SFamilyClass = reader.ReadInt16() ?? 0, // no classification
            Panose = ReadPanose(ref reader),
            UlUnicodeRange1 = reader.ReadUInt32() ?? 0,
            UlUnicodeRange2 = reader.ReadUInt32() ?? 0,
            UlUnicodeRange3 = reader.ReadUInt32() ?? 0,
            UlUnicodeRange4 = reader.ReadUInt32() ?? 0,
            AchVendId = new SfntTableTag(reader.ReadUInt32() ?? DefaultVendorId),
            FsSelection = reader.ReadUInt16() ?? DefaultFsSelection,
            UsFirstCharIndex = reader.ReadUInt16() ?? 0,
            UsLastCharIndex = reader.ReadUInt16() ?? DefaultLastCharIndex,
            STypoAscender = reader.ReadInt16() ?? 0,
            STypoDescender = reader.ReadInt16() ?? 0,
            STypoLineGap = reader.ReadInt16() ?? 0,
            UsWinAscent = reader.ReadUInt16() ?? 0,
            UsWinDescent = reader.ReadUInt16() ?? 0
        };

        if (os2.Version >= 1)
        {
            os2.UlCodePageRange1 = reader.ReadUInt32() ?? 0;
            os2.UlCodePageRange2 = reader.ReadUInt32() ?? 0;
        }

        if (os2.Version >= 2)
        {
            os2.SxHeight = reader.ReadInt16() ?? 0;
            os2.SCapHeight = reader.ReadInt16() ?? 0;
            os2.UsDefaultChar = reader.ReadUInt16() ?? 0;
            os2.UsBreakChar = reader.ReadUInt16() ?? DefaultBreakChar;
            os2.UsMaxContext = reader.ReadUInt16() ?? 0;
        }

        if (os2.Version == MaxSupportedVersion)
        {
            os2.UsLowerOpticalPointSize = reader.ReadUInt16() ?? 0;
            os2.UsUpperOpticalPointSize = reader.ReadUInt16() ?? DefaultUpperOpticalPointSize;
        }

        if (!reader.IsValid)
        {
            _logger.LogWarning(
                "Reading truncated 'OS/2' table: version {Version} needs more than the {ActualLength} bytes present; the fields past the end took their unspecified-value defaults.",
                os2.Version,
                data.Length);
        }

        return os2;
    }

    /// <summary>
    /// Creates a minimal placeholder "OS/2" table for a font that carries none: version 0, with every
    /// field at the value the spec treats as "unspecified" for it.
    /// </summary>
    public static byte[] CreateEmptyStub()
    {
        SfntOs2 os2 = new()
        {
            Version = 0,
            UsWeightClass = DefaultWeightClass,
            UsWidthClass = DefaultWidthClass,
            AchVendId = new SfntTableTag(DefaultVendorId),
            FsSelection = DefaultFsSelection,
            UsLastCharIndex = DefaultLastCharIndex
        };

        return WriteTable(os2);
    }

    /// <summary>
    /// Reads the 10-byte PANOSE classification, falling back to an all-zero one ("any" for every
    /// digit) when the data cannot carry it - the field is fixed-width, so a short read cannot be
    /// written back out as-is.
    /// </summary>
    private static byte[] ReadPanose(ref SfntReader reader)
    {
        ReadOnlySpan<byte> panose = reader.ReadBytes(PanoseLength);

        return (panose.Length == PanoseLength) ? panose.ToArray() : new byte[PanoseLength];
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

        return WriteTable(os2);
    }

    private static byte[] WriteTable(SfntOs2 os2)
    {
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
            return writer.Detach();
        }

        writer.WriteUInt32(os2.UlCodePageRange1.GetValueOrDefault());
        writer.WriteUInt32(os2.UlCodePageRange2.GetValueOrDefault());

        if (os2.Version == 1)
        {
            return writer.Detach();
        }

        writer.WriteInt16(os2.SxHeight.GetValueOrDefault());
        writer.WriteInt16(os2.SCapHeight.GetValueOrDefault());
        writer.WriteUInt16(os2.UsDefaultChar.GetValueOrDefault());
        writer.WriteUInt16(os2.UsBreakChar.GetValueOrDefault());
        writer.WriteUInt16(os2.UsMaxContext.GetValueOrDefault());

        if (os2.Version != 5)
        {
            return writer.Detach();
        }

        writer.WriteUInt16(os2.UsLowerOpticalPointSize.GetValueOrDefault());
        writer.WriteUInt16(os2.UsUpperOpticalPointSize.GetValueOrDefault());

        return writer.Detach();
    }
}
