using PdfPixel.Tiff.Model;
using System.IO;

namespace PdfPixel.Tiff.Parsing;

public static partial class TiffReader
{
    private static double[] ReadReals(in TiffSpanReader reader, in TiffEntry entry)
    {
        if (entry.Type != TiffFieldType.Rational && entry.Type != TiffFieldType.SignedRational)
        {
            long[] integers = ReadIntegers(reader, entry);
            var converted = new double[integers.Length];
            for (int index = 0; index < integers.Length; index++)
            {
                converted[index] = integers[index];
            }

            return converted;
        }

        var values = new double[entry.Count];

        for (int index = 0; index < values.Length; index++)
        {
            long offset = entry.ValueOffset + (index * 8L);
            uint numerator = reader.ReadUInt32(offset);
            uint denominator = reader.ReadUInt32(offset + 4);

            values[index] = (entry.Type == TiffFieldType.SignedRational)
                ? (double)(int)numerator / (int)denominator
                : (double)numerator / denominator;
        }

        return values;
    }

    private static int ReadInt32(in TiffSpanReader reader, in TiffEntry entry)
    {
        long[] values = ReadIntegers(reader, entry);
        return ToInt32(values[0], entry.Tag);
    }

    private static int[] ReadInt32Array(in TiffSpanReader reader, in TiffEntry entry)
    {
        long[] values = ReadIntegers(reader, entry);
        var result = new int[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            result[index] = ToInt32(values[index], entry.Tag);
        }

        return result;
    }

    private static ushort[] ReadUInt16Array(in TiffSpanReader reader, in TiffEntry entry)
    {
        long[] values = ReadIntegers(reader, entry);
        var result = new ushort[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            result[index] = (ushort)values[index];
        }

        return result;
    }

    private static TiffExtraSample[] ReadExtraSamples(in TiffSpanReader reader, in TiffEntry entry)
    {
        long[] values = ReadIntegers(reader, entry);
        var result = new TiffExtraSample[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            result[index] = (TiffExtraSample)values[index];
        }

        return result;
    }

    private static long[] ReadIntegers(in TiffSpanReader reader, in TiffEntry entry)
    {
        if (entry.Count == 0)
        {
            throw new InvalidDataException($"TIFF tag {entry.Tag} has no values.");
        }

        int typeSize = GetTypeSize(entry.Type);
        var values = new long[entry.Count];

        for (int index = 0; index < values.Length; index++)
        {
            long offset = entry.ValueOffset + (index * (long)typeSize);
            values[index] = entry.Type switch
            {
                TiffFieldType.Byte or TiffFieldType.Undefined => reader.ReadByte(offset),
                TiffFieldType.SignedByte => (sbyte)reader.ReadByte(offset),
                TiffFieldType.Short => reader.ReadUInt16(offset),
                TiffFieldType.SignedShort => (short)reader.ReadUInt16(offset),
                TiffFieldType.Long or TiffFieldType.Ifd => reader.ReadUInt32(offset),
                TiffFieldType.SignedLong => (int)reader.ReadUInt32(offset),
                TiffFieldType.Long8 or TiffFieldType.Ifd8 or TiffFieldType.SignedLong8 => (long)reader.ReadUInt64(offset),
                _ => throw new InvalidDataException($"TIFF tag {entry.Tag} has non-integer type {entry.Type}.")
            };
        }

        return values;
    }

    private static int ToInt32(long value, TiffTag tag)
    {
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new InvalidDataException($"TIFF tag {tag} value {value} is out of range.");
        }

        return (int)value;
    }

    private static long ReadOffset(in TiffSpanReader reader, long offset, bool isBigTiff)
    {
        if (!isBigTiff)
        {
            return reader.ReadUInt32(offset);
        }

        ulong value = reader.ReadUInt64(offset);
        if (value > long.MaxValue)
        {
            throw new InvalidDataException($"BigTIFF value {value} at offset {offset} is out of range.");
        }

        return (long)value;
    }

    private static int GetTypeSize(TiffFieldType type)
    {
        return type switch
        {
            TiffFieldType.Byte or TiffFieldType.Ascii or TiffFieldType.SignedByte or TiffFieldType.Undefined => 1,
            TiffFieldType.Short or TiffFieldType.SignedShort => 2,
            TiffFieldType.Long or TiffFieldType.SignedLong or TiffFieldType.Float or TiffFieldType.Ifd => 4,
            TiffFieldType.Rational or TiffFieldType.SignedRational or TiffFieldType.Double => 8,
            TiffFieldType.Long8 or TiffFieldType.SignedLong8 or TiffFieldType.Ifd8 => 8,
            _ => 0
        };
    }
}
