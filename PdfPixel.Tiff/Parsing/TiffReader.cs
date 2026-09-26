using PdfPixel.Tiff.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Tiff.Parsing;

/// <summary>
/// Parses the image file directories of classic TIFF and BigTIFF files.
/// </summary>
public static partial class TiffReader
{
    private const byte LittleEndianMarker = 0x49;
    private const byte BigEndianMarker = 0x4D;
    private const ushort ClassicVersion = 42;
    private const ushort BigTiffVersion = 43;
    private const ushort BigTiffOffsetSize = 8;
    private const int OldJpegQuantizationTableLength = 64;
    private const int OldJpegHuffmanCountsLength = 16;

    /// <summary>
    /// Reads every image file directory of a TIFF file, in file order.
    /// </summary>
    /// <param name="data">Complete TIFF file.</param>
    /// <returns>The directories the file links, the first one describing the first image.</returns>
    public static List<TiffDirectory> ReadDirectories(in ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            throw new InvalidDataException("TIFF file is shorter than its header.");
        }

        bool isLittleEndian;
        if (data[0] == LittleEndianMarker && data[1] == LittleEndianMarker)
        {
            isLittleEndian = true;
        }
        else if (data[0] == BigEndianMarker && data[1] == BigEndianMarker)
        {
            isLittleEndian = false;
        }
        else
        {
            throw new InvalidDataException("TIFF header has no byte order mark.");
        }

        TiffSpanReader reader = new(data, isLittleEndian);
        ushort version = reader.ReadUInt16(2);

        bool isBigTiff;
        long directoryOffset;
        if (version == ClassicVersion)
        {
            isBigTiff = false;
            directoryOffset = reader.ReadUInt32(4);
        }
        else if (version == BigTiffVersion)
        {
            if (reader.ReadUInt16(4) != BigTiffOffsetSize)
            {
                throw new InvalidDataException("BigTIFF header declares an offset size other than 8.");
            }

            isBigTiff = true;
            directoryOffset = ReadOffset(reader, 8, isBigTiff);
        }
        else
        {
            throw new InvalidDataException($"TIFF header has unknown version {version}.");
        }

        return ReadDirectoryChain(reader, directoryOffset, isBigTiff, []);
    }

    private static List<TiffDirectory> ReadDirectoryChain(in TiffSpanReader reader, long directoryOffset, bool isBigTiff, HashSet<long> visitedOffsets)
    {
        List<TiffDirectory> directories = [];

        while (directoryOffset != 0 && visitedOffsets.Add(directoryOffset))
        {
            directories.Add(ReadDirectory(reader, directoryOffset, isBigTiff, visitedOffsets, out directoryOffset));
        }

        return directories;
    }

    private static TiffDirectory ReadDirectory(
        in TiffSpanReader reader,
        long directoryOffset,
        bool isBigTiff,
        HashSet<long> visitedOffsets,
        out long nextDirectoryOffset)
    {
        int countSize = isBigTiff ? 8 : 2;
        int entrySize = isBigTiff ? 20 : 12;
        int valueFieldSize = isBigTiff ? 8 : 4;

        long entryCount = isBigTiff
            ? ReadOffset(reader, directoryOffset, isBigTiff)
            : reader.ReadUInt16(directoryOffset);

        if (entryCount < 0 || entryCount * entrySize > reader.Length)
        {
            throw new InvalidDataException($"TIFF directory at offset {directoryOffset} declares {entryCount} entries.");
        }

        TiffDirectory directory = new() { IsLittleEndian = reader.IsLittleEndian };
        long[]? subDirectoryOffsets = null;
        long? interchangeFormatOffset = null;
        long? interchangeFormatLength = null;
        long[]? quantizationTableOffsets = null;
        long[]? dcTableOffsets = null;
        long[]? acTableOffsets = null;

        for (long entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            long entryOffset = directoryOffset + countSize + (entryIndex * entrySize);
            var tag = (TiffTag)reader.ReadUInt16(entryOffset);
            var type = (TiffFieldType)reader.ReadUInt16(entryOffset + 2);
            long count = isBigTiff
                ? ReadOffset(reader, entryOffset + 4, isBigTiff)
                : reader.ReadUInt32(entryOffset + 4);
            long valueFieldOffset = entryOffset + 4 + valueFieldSize;

            int typeSize = GetTypeSize(type);
            if (typeSize == 0 || count < 0 || count > reader.Length)
            {
                continue;
            }

            long valueOffset = (count * typeSize <= valueFieldSize)
                ? valueFieldOffset
                : ReadOffset(reader, valueFieldOffset, isBigTiff);

            TiffEntry entry = new(tag, type, count, valueOffset);

            switch (tag)
            {
                case TiffTag.SubIfds:
                    {
                        subDirectoryOffsets = ReadIntegers(reader, entry);
                        break;
                    }
                case TiffTag.JpegInterchangeFormat:
                    {
                        interchangeFormatOffset = ReadIntegers(reader, entry)[0];
                        break;
                    }
                case TiffTag.JpegInterchangeFormatLength:
                    {
                        interchangeFormatLength = ReadIntegers(reader, entry)[0];
                        break;
                    }
                case TiffTag.JpegQTables:
                    {
                        quantizationTableOffsets = ReadIntegers(reader, entry);
                        break;
                    }
                case TiffTag.JpegDcTables:
                    {
                        dcTableOffsets = ReadIntegers(reader, entry);
                        break;
                    }
                case TiffTag.JpegAcTables:
                    {
                        acTableOffsets = ReadIntegers(reader, entry);
                        break;
                    }
                default:
                    {
                        ApplyEntry(directory, reader, entry);
                        break;
                    }
            }
        }

        nextDirectoryOffset = ReadOffset(reader, directoryOffset + countSize + (entryCount * entrySize), isBigTiff);

        if (interchangeFormatOffset.HasValue || quantizationTableOffsets != null || dcTableOffsets != null || acTableOffsets != null)
        {
            TiffOldJpegParameters oldJpeg = GetOldJpeg(directory);

            if (interchangeFormatOffset.HasValue)
            {
                long length = interchangeFormatLength ?? (reader.Length - interchangeFormatOffset.Value);
                oldJpeg.InterchangeFormat = reader.Slice(interchangeFormatOffset.Value, length).ToArray();
            }

            if (quantizationTableOffsets != null)
            {
                oldJpeg.QuantizationTables = ReadOldJpegQuantizationTables(reader, quantizationTableOffsets);
            }

            if (dcTableOffsets != null)
            {
                oldJpeg.DcHuffmanTables = ReadOldJpegHuffmanTables(reader, dcTableOffsets);
            }

            if (acTableOffsets != null)
            {
                oldJpeg.AcHuffmanTables = ReadOldJpegHuffmanTables(reader, acTableOffsets);
            }
        }

        if (subDirectoryOffsets != null)
        {
            for (int index = 0; index < subDirectoryOffsets.Length; index++)
            {
                directory.SubDirectories.AddRange(ReadDirectoryChain(reader, subDirectoryOffsets[index], isBigTiff, visitedOffsets));
            }
        }

        return directory;
    }
}
