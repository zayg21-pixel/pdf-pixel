using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes an SFNT container's header and table directory. Table-content interpretation
/// (cmap, glyf, CFF, ...) is handled by separate per-table processors.
/// </summary>
public class SfntContainerProcessor
{
    private const int HeaderLength = 12;
    private const int DirectoryEntryLength = 16;
    private const uint ChecksumAdjustmentMagic = 0xB1B0AFBA;

    private readonly ILogger<SfntContainerProcessor> _logger;

    private readonly struct DirectoryParams
    {
        public DirectoryParams(ushort searchRange, ushort entrySelector, ushort rangeShift)
        {
            SearchRange = searchRange;
            EntrySelector = entrySelector;
            RangeShift = rangeShift;
        }

        public ushort SearchRange { get; }

        public ushort EntrySelector { get; }

        public ushort RangeShift { get; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntContainerProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntContainerProcessor(ILogger<SfntContainerProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses the sfnt header and table directory from <paramref name="data"/>, slicing out each
    /// table's raw content. Returns null if the header, directory, or any table does not fit within
    /// <paramref name="data"/>.
    /// </summary>
    public SfntContainer? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            _logger.LogWarning("Failed to read sfnt header: expected at least {ExpectedLength} bytes, got {ActualLength}.", HeaderLength, data.Length);
            return null;
        }

        SfntReader reader = new(data.Span);
        uint version = reader.ReadUInt32OrDefault();
        ushort numTables = reader.ReadUInt16OrDefault();
        reader.Skip(6); // searchRange, entrySelector, rangeShift

        long directoryEnd = HeaderLength + ((long)numTables * DirectoryEntryLength);
        if (directoryEnd > data.Length)
        {
            _logger.LogWarning("Failed to read sfnt table directory: {NumTables} tables need {DirectoryEnd} bytes, only {ActualLength} available.", numTables, directoryEnd, data.Length);
            return null;
        }

        var tables = new SfntTableRecord[numTables];
        for (int tableIndex = 0; tableIndex < numTables; tableIndex++)
        {
            uint tagValue = reader.ReadUInt32OrDefault();
            uint checkSum = reader.ReadUInt32OrDefault();
            uint tableOffset = reader.ReadUInt32OrDefault();
            uint tableLength = reader.ReadUInt32OrDefault();

            if ((long)tableOffset + tableLength > data.Length)
            {
                _logger.LogWarning(
                    "Failed to read sfnt table '{Tag}': offset {Offset} + length {Length} exceeds font data length {ActualLength}.",
                    new SfntTableTag(tagValue),
                    tableOffset,
                    tableLength,
                    data.Length);
                return null;
            }

            ReadOnlyMemory<byte> tableData = data.Slice((int)tableOffset, (int)tableLength);
            tables[tableIndex] = new SfntTableRecord(new SfntTableTag(tagValue), checkSum, tableData);
        }

        return new SfntContainer(version, tables);
    }

    /// <summary>
    /// Assembles a full sfnt font from its version tag and tables: sorts the tables by tag, lays out
    /// the header and directory, computes every table's checksum, and - if a "head" table is present -
    /// patches its checkSumAdjustment field once the whole font's checksum is known.
    /// </summary>
    public byte[] Write(uint version, IReadOnlyList<SfntTableRecord> tables)
    {
        if (tables == null)
        {
            throw new ArgumentNullException(nameof(tables));
        }

        List<SfntTableRecord> sortedTables = new(tables);
        sortedTables.Sort((left, right) => left.Tag.Value.CompareTo(right.Tag.Value));

        var tableCount = (ushort)sortedTables.Count;
        DirectoryParams directoryParams = ComputeDirParams(tableCount);

        var tableOffsets = new int[sortedTables.Count];
        int offset = HeaderLength + (sortedTables.Count * DirectoryEntryLength);
        for (int tableIndex = 0; tableIndex < sortedTables.Count; tableIndex++)
        {
            offset = Align4(offset);
            tableOffsets[tableIndex] = offset;
            offset += Align4(sortedTables[tableIndex].Data.Length);
        }

        byte[] fontBytes = BuildFontBytes(version, sortedTables, tableOffsets, directoryParams);

        int headTableIndex = sortedTables.FindIndex(table => table.Tag == SfntTableTags.Head);
        if (headTableIndex < 0)
        {
            return fontBytes;
        }

        uint totalChecksum = CalcChecksum(fontBytes);
        uint checksumAdjustment = unchecked(ChecksumAdjustmentMagic - totalChecksum);
        int checksumAdjustmentOffset = tableOffsets[headTableIndex] + SfntHeadProcessor.CheckSumAdjustmentOffset;
        WriteUInt32BigEndian(fontBytes, checksumAdjustmentOffset, checksumAdjustment);

        return fontBytes;
    }

    private static byte[] BuildFontBytes(uint version, List<SfntTableRecord> sortedTables, int[] tableOffsets, in DirectoryParams directoryParams)
    {
        SfntWriter writer = new();

        writer.WriteUInt32(version);
        writer.WriteUInt16((ushort)sortedTables.Count);
        writer.WriteUInt16(directoryParams.SearchRange);
        writer.WriteUInt16(directoryParams.EntrySelector);
        writer.WriteUInt16(directoryParams.RangeShift);

        for (int tableIndex = 0; tableIndex < sortedTables.Count; tableIndex++)
        {
            SfntTableRecord table = sortedTables[tableIndex];
            writer.WriteUInt32(table.Tag.Value);
            writer.WriteUInt32(CalcChecksum(table.Data.Span));
            writer.WriteUInt32((uint)tableOffsets[tableIndex]);
            writer.WriteUInt32((uint)table.Data.Length);
        }

        for (int tableIndex = 0; tableIndex < sortedTables.Count; tableIndex++)
        {
            SfntTableRecord table = sortedTables[tableIndex];
            while (writer.Length < tableOffsets[tableIndex])
            {
                writer.WriteByte(0);
            }

            writer.WriteBytes(table.Data.Span);

            int paddedLength = Align4(table.Data.Length);
            for (int padIndex = table.Data.Length; padIndex < paddedLength; padIndex++)
            {
                writer.WriteByte(0);
            }
        }

        return writer.ToArray();
    }

    private static int Align4(int value) => (value + 3) & ~3;

    private static uint CalcChecksum(in ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        int length = Align4(data.Length);
        for (int index = 0; index < length; index += 4)
        {
            uint word = 0;
            if (index < data.Length)
            {
                word |= (uint)data[index] << 24;
            }

            if (index + 1 < data.Length)
            {
                word |= (uint)data[index + 1] << 16;
            }

            if (index + 2 < data.Length)
            {
                word |= (uint)data[index + 2] << 8;
            }

            if (index + 3 < data.Length)
            {
                word |= data[index + 3];
            }

            sum += word;
        }

        return sum;
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static DirectoryParams ComputeDirParams(ushort numTables)
    {
        ushort maxPowerOfTwo = 1;
        ushort entrySelector = 0;
        while ((maxPowerOfTwo << 1) <= numTables)
        {
            maxPowerOfTwo <<= 1;
            entrySelector++;
        }

        var searchRange = (ushort)(maxPowerOfTwo * 16);
        var rangeShift = (ushort)((numTables * 16) - searchRange);

        return new DirectoryParams(searchRange, entrySelector, rangeShift);
    }
}
