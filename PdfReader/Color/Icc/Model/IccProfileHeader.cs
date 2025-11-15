using PdfReader.Color.Icc.Utilities;
using System;

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Represents the fixed 128-byte ICC profile header (only the subset of fields required by the PDF color workflows).
/// See ICC specification (v4) for authoritative field definitions and layout.
/// </summary>
internal sealed class IccProfileHeader
{
    // Header field offsets (byte positions from start of profile file / header block)
    private const int OffsetSize = 0;              // uInt32 profile size
    private const int OffsetCmmType = 4;           // signature of preferred CMM
    private const int OffsetVersion = 8;           // uInt32 (major(8) . minor nibble . bugfix nibble)
    private const int OffsetDeviceClass = 12;      // profile / device class 4CC
    private const int OffsetColorSpace = 16;       // color space of data 4CC
    private const int OffsetPcs = 20;              // PCS 4CC
    private const int OffsetCreationDate = 24;     // dateTimeNumber (year..second) 6 * uInt16
    private const int OffsetSignature = 36;        // profile file signature (expected 'acsp') – currently ignored
    private const int OffsetPlatform = 40;         // primary platform
    private const int OffsetFlags = 44;            // profile flags
    private const int OffsetDeviceManufacturer = 48; // manufacturer 4CC
    private const int OffsetDeviceModel = 52;      // model (uInt32)
    private const int OffsetAttributes = 56;       // device attributes (uInt64) – currently not exposed
    private const int OffsetRenderingIntent = 64;  // rendering intent (uInt32)
    private const int OffsetIlluminant = 68;       // XYZ of profile illuminant (3 * s15Fixed16)
    private const int OffsetCreator = 80;          // profile creator signature

    /// <summary>
    /// Total size of the ICC profile in bytes.
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Preferred CMM type signature (4-character code).
    /// </summary>
    public string CmmType { get; set; }

    /// <summary>
    /// Parsed profile version (major.minor.bugfix per ICC packed encoding).
    /// </summary>
    public Version Version { get; set; }

    /// <summary>
    /// Device / profile class signature (e.g. 'mntr', 'prtr', 'spac').
    /// </summary>
    public string DeviceClass { get; set; }

    /// <summary>
    /// Data color space signature (e.g. 'RGB ', 'CMYK', 'GRAY').
    /// </summary>
    public string ColorSpace { get; set; }

    /// <summary>
    /// Profile Connection Space (PCS) signature (e.g. 'XYZ ', 'Lab ').
    /// </summary>
    public string Pcs { get; set; }

    /// <summary>
    /// Profile creation timestamp (UTC) if valid; otherwise null when date/time fields are out-of-range.
    /// </summary>
    public DateTime? CreationTime { get; set; }

    /// <summary>
    /// Primary platform signature (e.g. 'MSFT', 'APPL').
    /// </summary>
    public string Platform { get; set; }

    /// <summary>
    /// Raw profile flags bitfield (implementation specific usage).
    /// </summary>
    public uint FlagsRaw { get; set; }

    /// <summary>
    /// Device manufacturer signature (4-character code).
    /// </summary>
    public string DeviceManufacturer { get; set; }

    /// <summary>
    /// Device model code (raw uInt32 as stored in header).
    /// </summary>
    public uint DeviceModel { get; set; }

    /// <summary>
    /// Rendering intent declared in the profile header (0..3 correspond to perceptual, relative colorimetric, saturation, absolute colorimetric).
    /// </summary>
    public uint RenderingIntent { get; set; }

    /// <summary>
    /// Profile illuminant XYZ (typically D50) if valid.
    /// </summary>
    public IccXyz? Illuminant { get; set; }

    /// <summary>
    /// Profile creator signature (4-character code identifying the creator application/tool).
    /// </summary>
    public string Creator { get; set; }

    /// <summary>
    /// Read the ICC profile header fields from the provided big-endian reader.
    /// Caller must ensure the underlying data contains at least the 128-byte header.
    /// </summary>
    /// <param name="reader">Big endian reader positioned at the start of profile data.</param>
    /// <returns>A populated <see cref="IccProfileHeader"/> instance.</returns>
    public static IccProfileHeader Read(BigEndianReader reader)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        // Basic bounds guard – standard ICC header is 128 bytes.
        if (!reader.CanRead(0, 128))
        {
            throw new ArgumentException("ICC profile header truncated or missing.", nameof(reader));
        }

        var header = new IccProfileHeader();

        uint size = reader.ReadUInt32(OffsetSize);
        uint versionPacked = reader.ReadUInt32(OffsetVersion);

        // Date/time components (six unsigned 16-bit values)
        int year = reader.ReadUInt16(OffsetCreationDate + 0);
        int month = reader.ReadUInt16(OffsetCreationDate + 2);
        int day = reader.ReadUInt16(OffsetCreationDate + 4);
        int hour = reader.ReadUInt16(OffsetCreationDate + 6);
        int minute = reader.ReadUInt16(OffsetCreationDate + 8);
        int second = reader.ReadUInt16(OffsetCreationDate + 10);

        DateTime? created = null;
        try
        {
            created = new DateTime(
                year,
                Math.Max(1, month),
                Math.Max(1, day),
                Math.Min(23, hour),
                Math.Min(59, minute),
                Math.Min(59, second),
                DateTimeKind.Utc);
        }
        catch
        {
            // Keep null when invalid – no rethrow (spec allows zeros in some fields).
        }

        // Illuminant (XYZ, s15Fixed16 each)
        int ix = reader.ReadInt32(OffsetIlluminant + 0);
        int iy = reader.ReadInt32(OffsetIlluminant + 4);
        int iz = reader.ReadInt32(OffsetIlluminant + 8);
        IccXyz? illuminant = null;
        try
        {
            illuminant = new IccXyz(
                BigEndianReader.S15Fixed16ToSingle(ix),
                BigEndianReader.S15Fixed16ToSingle(iy),
                BigEndianReader.S15Fixed16ToSingle(iz));
        }
        catch
        {
            // Leave null if conversion invalid.
        }

        header.Size = size;
        header.CmmType = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetCmmType));
        header.Version = new Version(
            (int)(versionPacked >> 24 & 0xFF),
            (int)(versionPacked >> 20 & 0x0F),
            (int)(versionPacked >> 16 & 0x0F));
        header.DeviceClass = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetDeviceClass));
        header.ColorSpace = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetColorSpace));
        header.Pcs = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetPcs));
        header.CreationTime = created;
        // Signature at OffsetSignature is typically 'acsp'; we currently ignore verifying it for leniency.
        header.Platform = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetPlatform));
        header.FlagsRaw = reader.ReadUInt32(OffsetFlags);
        header.DeviceManufacturer = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetDeviceManufacturer));
        header.DeviceModel = reader.ReadUInt32(OffsetDeviceModel);
        // Device attributes (uInt64) at OffsetAttributes currently unused but consumed to advance.
        _ = reader.ReadUInt64(OffsetAttributes);
        header.RenderingIntent = reader.ReadUInt32(OffsetRenderingIntent);
        header.Illuminant = illuminant;
        header.Creator = BigEndianReader.FourCCToString(reader.ReadUInt32(OffsetCreator));

        return header;
    }
}
