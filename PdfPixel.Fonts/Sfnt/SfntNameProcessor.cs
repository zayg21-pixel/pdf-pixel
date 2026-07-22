using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "name" table (format 0 only - format 1's additional
/// language-tag records are legacy and effectively unused in practice).
/// </summary>
public class SfntNameProcessor
{
    private const int HeaderLength = 6;
    private const int RecordLength = 12;

    private readonly ILogger<SfntNameProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntNameProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntNameProcessor(ILogger<SfntNameProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "name" table from its raw content bytes. Returns null if it is shorter than the
    /// fixed 6-byte header. Individual records that don't fit are skipped rather than failing the
    /// whole table.
    /// </summary>
    public SfntName? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            _logger.LogWarning("Failed to read 'name' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", HeaderLength, data.Length);
            return null;
        }

        SfntReader headerReader = new(data.Span);
        headerReader.Skip(2); // format
        ushort recordCount = headerReader.ReadUInt16OrDefault();
        ushort storageOffset = headerReader.ReadUInt16OrDefault();

        List<SfntNameRecord> records = new(recordCount);
        for (int recordIndex = 0; recordIndex < recordCount; recordIndex++)
        {
            SfntReader recordReader = new(data.Span);
            int recordOffset = HeaderLength + (recordIndex * RecordLength);
            if (!recordReader.Seek(recordOffset))
            {
                _logger.LogWarning("Failed to read 'name' record {RecordIndex}: record offset {RecordOffset} is out of bounds.", recordIndex, recordOffset);
                break;
            }

            ushort platformId = recordReader.ReadUInt16OrDefault();
            ushort encodingId = recordReader.ReadUInt16OrDefault();
            ushort languageId = recordReader.ReadUInt16OrDefault();
            ushort nameId = recordReader.ReadUInt16OrDefault();
            ushort length = recordReader.ReadUInt16OrDefault();
            ushort valueOffset = recordReader.ReadUInt16OrDefault();

            if (!recordReader.IsValid)
            {
                _logger.LogWarning("Failed to read 'name' record {RecordIndex}: record is truncated.", recordIndex);
                break;
            }

            int valueStart = storageOffset + valueOffset;
            if (valueStart + length > data.Length)
            {
                _logger.LogWarning("Failed to read 'name' record {RecordIndex}: string bytes exceed table length.", recordIndex);
                continue;
            }

            ReadOnlyMemory<byte> value = data.Slice(valueStart, length);
            records.Add(new SfntNameRecord(platformId, encodingId, languageId, nameId, value));
        }

        return new SfntName { Records = records };
    }

    /// <summary>
    /// Writes a "name" table's binary content (format 0). Records are sorted by
    /// (platformID, encodingID, languageID, nameID), as the spec requires.
    /// </summary>
    public byte[] Write(SfntName name)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        List<SfntNameRecord> sortedRecords = new(name.Records);
        sortedRecords.Sort(CompareRecords);

        var recordCount = (ushort)sortedRecords.Count;
        var storageOffset = (ushort)(HeaderLength + (recordCount * RecordLength));

        SfntWriter writer = new();
        writer.WriteUInt16(0); // format
        writer.WriteUInt16(recordCount);
        writer.WriteUInt16(storageOffset);

        int currentValueOffset = 0;
        for (int recordIndex = 0; recordIndex < sortedRecords.Count; recordIndex++)
        {
            SfntNameRecord record = sortedRecords[recordIndex];
            writer.WriteUInt16(record.PlatformId);
            writer.WriteUInt16(record.EncodingId);
            writer.WriteUInt16(record.LanguageId);
            writer.WriteUInt16(record.NameId);
            writer.WriteUInt16((ushort)record.Value.Length);
            writer.WriteUInt16((ushort)currentValueOffset);
            currentValueOffset += record.Value.Length;
        }

        for (int recordIndex = 0; recordIndex < sortedRecords.Count; recordIndex++)
        {
            writer.WriteBytes(sortedRecords[recordIndex].Value.Span);
        }

        return writer.ToArray();
    }

    private static int CompareRecords(SfntNameRecord left, SfntNameRecord right)
    {
        int comparison = left.PlatformId.CompareTo(right.PlatformId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.EncodingId.CompareTo(right.EncodingId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.LanguageId.CompareTo(right.LanguageId);
        if (comparison != 0)
        {
            return comparison;
        }

        return left.NameId.CompareTo(right.NameId);
    }
}
