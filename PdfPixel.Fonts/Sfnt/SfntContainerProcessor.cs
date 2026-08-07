using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;
using System.IO;

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
    private const uint TtcTag = 0x74746366; // 'ttcf'
    private const int TtcHeaderLength = 12;

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
    /// Parses the sfnt header and table directory from <paramref name="stream"/>, at the given font
    /// index within a TrueType Collection ("ttcf") file - ignored if <paramref name="stream"/> is a
    /// plain (non-collection) sfnt file. Records every table's byte range without reading its content.
    /// Returns null if the header or directory does not fit within <paramref name="stream"/>; a single
    /// table whose range does not fit is left out of the container.
    /// </summary>
    public SfntContainer? Read(ReadOnlyFontStream stream, int ttcIndex) => Read(stream, AllTables, ttcIndex);

    /// <summary>
    /// Parses the sfnt header and table directory from <paramref name="stream"/>, recording the byte
    /// range of each table for which <paramref name="tableFilter"/> returns <see langword="true"/>,
    /// without reading its content. Returns null if the header or directory does not fit within
    /// <paramref name="stream"/>; a single table whose range does not fit is left out of the container.
    /// </summary>
    public SfntContainer? Read(ReadOnlyFontStream stream, Func<SfntTableTag, bool> tableFilter) => Read(stream, tableFilter, ttcIndex: 0);

    /// <summary>
    /// Parses the sfnt header and table directory from <paramref name="stream"/>, at the given font
    /// index within a TrueType Collection ("ttcf") file - ignored if <paramref name="stream"/> is a
    /// plain (non-collection) sfnt file. Records the byte range of each table for which
    /// <paramref name="tableFilter"/> returns <see langword="true"/>, without reading its content.
    /// Returns null if the header or directory does not fit within <paramref name="stream"/>; a single
    /// table whose range does not fit is left out of the container.
    /// </summary>
    public SfntContainer? Read(ReadOnlyFontStream stream, Func<SfntTableTag, bool> tableFilter, int ttcIndex)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (tableFilter == null)
        {
            throw new ArgumentNullException(nameof(tableFilter));
        }

        int headerStart = 0;
        if (stream.Length >= TtcHeaderLength)
        {
            SfntReader tagReader = new(stream.GetMemory(0, 4).Span);
            if (tagReader.ReadUInt32OrDefault() == TtcTag)
            {
                int? fontOffset = ReadTtcFontOffset(stream, ttcIndex);
                if (fontOffset == null)
                {
                    return null;
                }

                headerStart = fontOffset.Value;
            }
        }

        if (stream.Length < headerStart + HeaderLength)
        {
            _logger.LogWarning("Failed to read sfnt header: expected at least {ExpectedLength} bytes, got {ActualLength}.", headerStart + HeaderLength, stream.Length);
            return null;
        }

        SfntReader headerReader = new(stream.GetMemory(headerStart, HeaderLength).Span);
        uint version = headerReader.ReadUInt32OrDefault();
        ushort numTables = headerReader.ReadUInt16OrDefault();
        headerReader.Skip(6); // searchRange, entrySelector, rangeShift

        int directoryStart = headerStart + HeaderLength;
        long directoryEnd = directoryStart + ((long)numTables * DirectoryEntryLength);
        if (directoryEnd > stream.Length)
        {
            _logger.LogWarning("Failed to read sfnt table directory: {NumTables} tables need {DirectoryEnd} bytes, only {ActualLength} available.", numTables, directoryEnd, stream.Length);
            return null;
        }

        SfntReader directoryReader = new(stream.GetMemory(directoryStart, numTables * DirectoryEntryLength).Span);
        List<SfntTableRecord> tables = new(numTables);
        for (int tableIndex = 0; tableIndex < numTables; tableIndex++)
        {
            uint tagValue = directoryReader.ReadUInt32OrDefault();
            uint checkSum = directoryReader.ReadUInt32OrDefault();
            uint tableOffset = directoryReader.ReadUInt32OrDefault();
            uint tableLength = directoryReader.ReadUInt32OrDefault();
            SfntTableTag tag = new(tagValue);

            if (!tableFilter(tag))
            {
                continue;
            }

            if ((long)tableOffset + tableLength > stream.Length)
            {
                _logger.LogWarning(
                    "Skipped sfnt table '{Tag}': offset {Offset} + length {Length} exceeds font data length {ActualLength}.",
                    tag,
                    tableOffset,
                    tableLength,
                    stream.Length);
                continue;
            }

            tables.Add(new SfntTableRecord(tag, checkSum, (int)tableOffset, (int)tableLength));
        }

        return new SfntContainer(version, tables);
    }

    private static bool AllTables(SfntTableTag tag) => true;

    private int? ReadTtcFontOffset(ReadOnlyFontStream stream, int ttcIndex)
    {
        SfntReader ttcHeaderReader = new(stream.GetMemory(0, TtcHeaderLength).Span);
        ttcHeaderReader.Skip(8); // ttcTag, majorVersion, minorVersion
        uint numFonts = ttcHeaderReader.ReadUInt32OrDefault();

        if (ttcIndex < 0 || ttcIndex >= numFonts)
        {
            _logger.LogWarning("Failed to read TrueType Collection: index {TtcIndex} is out of range for {NumFonts} fonts.", ttcIndex, numFonts);
            return null;
        }

        long offsetEntryPosition = TtcHeaderLength + ((long)ttcIndex * 4);
        if (offsetEntryPosition + 4 > stream.Length)
        {
            _logger.LogWarning(
                "Failed to read TrueType Collection: offset table entry for index {TtcIndex} exceeds font data length {ActualLength}.",
                ttcIndex,
                stream.Length);
            return null;
        }

        SfntReader offsetReader = new(stream.GetMemory((int)offsetEntryPosition, 4).Span);
        return (int)offsetReader.ReadUInt32OrDefault();
    }

    /// <summary>
    /// Assembles a full sfnt font from its version tag and tables: sorts the tables by tag, lays out
    /// the header and directory, computes every table's checksum, and - if a "head" table is present -
    /// patches its checkSumAdjustment field once the whole font's checksum is known.
    /// </summary>
    public byte[] Write(uint version, IReadOnlyList<SfntTableData> tables)
    {
        if (tables == null)
        {
            throw new ArgumentNullException(nameof(tables));
        }

        List<SfntTableData> sortedTables = new(tables);
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

    private static byte[] BuildFontBytes(uint version, List<SfntTableData> sortedTables, int[] tableOffsets, in DirectoryParams directoryParams)
    {
        SfntWriter writer = new();

        writer.WriteUInt32(version);
        writer.WriteUInt16((ushort)sortedTables.Count);
        writer.WriteUInt16(directoryParams.SearchRange);
        writer.WriteUInt16(directoryParams.EntrySelector);
        writer.WriteUInt16(directoryParams.RangeShift);

        for (int tableIndex = 0; tableIndex < sortedTables.Count; tableIndex++)
        {
            SfntTableData table = sortedTables[tableIndex];
            writer.WriteUInt32(table.Tag.Value);
            writer.WriteUInt32(CalcChecksum(table.Data.Span));
            writer.WriteUInt32((uint)tableOffsets[tableIndex]);
            writer.WriteUInt32((uint)table.Data.Length);
        }

        for (int tableIndex = 0; tableIndex < sortedTables.Count; tableIndex++)
        {
            SfntTableData table = sortedTables[tableIndex];
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
