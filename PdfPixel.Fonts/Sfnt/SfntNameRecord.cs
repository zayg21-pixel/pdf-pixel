using System;
using System.Text;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single record within the SFNT "name" table. <see cref="Value"/> is the record's raw, undecoded
/// string bytes - the text encoding depends on <see cref="PlatformId"/>/<see cref="EncodingId"/> (most
/// commonly big-endian UTF-16 for platform 3 "Windows" records), which this type does not interpret.
/// </summary>
public readonly struct SfntNameRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntNameRecord"/> struct.
    /// </summary>
    /// <param name="platformId">The platform ID (e.g. 1 = Macintosh, 3 = Windows).</param>
    /// <param name="encodingId">The platform-specific encoding ID.</param>
    /// <param name="languageId">The platform-specific language ID.</param>
    /// <param name="nameId">The name ID identifying what this record contains.</param>
    /// <param name="value">The record's raw, undecoded string bytes.</param>
    public SfntNameRecord(ushort platformId, ushort encodingId, ushort languageId, ushort nameId, in ReadOnlyMemory<byte> value)
    {
        PlatformId = platformId;
        EncodingId = encodingId;
        LanguageId = languageId;
        NameId = nameId;
        Value = value;
    }

    /// <summary>
    /// Gets the platform ID (e.g. 1 = Macintosh, 3 = Windows).
    /// </summary>
    public ushort PlatformId { get; }

    /// <summary>
    /// Gets the platform-specific encoding ID.
    /// </summary>
    public ushort EncodingId { get; }

    /// <summary>
    /// Gets the platform-specific language ID (e.g. 0x0409 = English/US for Windows records).
    /// </summary>
    public ushort LanguageId { get; }

    /// <summary>
    /// Gets the name ID identifying what this record contains (e.g. 1 = font family, 4 = full name).
    /// </summary>
    public ushort NameId { get; }

    /// <summary>
    /// Gets the record's raw, undecoded string bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// Creates a Windows-platform (platformId 3), Unicode BMP (encodingId 1) record from a string,
    /// encoded as big-endian UTF-16 as that platform/encoding requires.
    /// </summary>
    /// <param name="languageId">The record's language ID (e.g. 0x0409 for English/US).</param>
    /// <param name="nameId">The name ID identifying what this record contains.</param>
    /// <param name="value">The string to encode.</param>
    public static SfntNameRecord CreateWindowsUnicode(ushort languageId, ushort nameId, string value)
    {
        byte[] bytes = Encoding.BigEndianUnicode.GetBytes(value);
        return new SfntNameRecord(platformId: 3, encodingId: 1, languageId, nameId, bytes);
    }
}
