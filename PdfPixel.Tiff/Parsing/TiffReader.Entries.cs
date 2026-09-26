using PdfPixel.Tiff.Model;
using System;

namespace PdfPixel.Tiff.Parsing;

public static partial class TiffReader
{
    private static void ApplyEntry(TiffDirectory directory, in TiffSpanReader reader, in TiffEntry entry)
    {
        switch (entry.Tag)
        {
            case TiffTag.NewSubfileType:
                {
                    directory.SubfileType = (TiffSubfileType)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.ImageWidth:
                {
                    directory.Width = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.ImageLength:
                {
                    directory.Height = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.BitsPerSample:
                {
                    directory.BitsPerSample = ReadInt32Array(reader, entry);
                    break;
                }
            case TiffTag.Compression:
                {
                    directory.Compression = (TiffCompression)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.PhotometricInterpretation:
                {
                    directory.Photometric = (TiffPhotometric)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.FillOrder:
                {
                    directory.FillOrder = (TiffFillOrder)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.StripOffsets:
                {
                    directory.StripOffsets = ReadIntegers(reader, entry);
                    break;
                }
            case TiffTag.Orientation:
                {
                    directory.Orientation = (TiffOrientation)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.SamplesPerPixel:
                {
                    directory.SamplesPerPixel = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.RowsPerStrip:
                {
                    directory.RowsPerStrip = ReadIntegers(reader, entry)[0];
                    break;
                }
            case TiffTag.StripByteCounts:
                {
                    directory.StripByteCounts = ReadIntegers(reader, entry);
                    break;
                }
            case TiffTag.PlanarConfiguration:
                {
                    directory.PlanarConfiguration = (TiffPlanarConfiguration)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.T4Options:
                {
                    directory.T4Options = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.Predictor:
                {
                    directory.Predictor = (TiffPredictor)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.ColorMap:
                {
                    directory.ColorMap = ReadUInt16Array(reader, entry);
                    break;
                }
            case TiffTag.TileWidth:
                {
                    directory.TileWidth = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.TileLength:
                {
                    directory.TileHeight = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.TileOffsets:
                {
                    directory.TileOffsets = ReadIntegers(reader, entry);
                    break;
                }
            case TiffTag.TileByteCounts:
                {
                    directory.TileByteCounts = ReadIntegers(reader, entry);
                    break;
                }
            case TiffTag.ExtraSamples:
                {
                    directory.ExtraSamples = ReadExtraSamples(reader, entry);
                    break;
                }
            case TiffTag.SampleFormat:
                {
                    directory.SampleFormat = (TiffSampleFormat)ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.JpegTables:
                {
                    directory.JpegTables = reader.Slice(entry.ValueOffset, entry.Count).ToArray();
                    break;
                }
            case TiffTag.YCbCrCoefficients:
                {
                    directory.YCbCrCoefficients = ReadReals(reader, entry);
                    break;
                }
            case TiffTag.YCbCrSubSampling:
                {
                    directory.YCbCrSubsampling = ReadInt32Array(reader, entry);
                    break;
                }
            case TiffTag.ReferenceBlackWhite:
                {
                    directory.ReferenceBlackWhite = ReadReals(reader, entry);
                    break;
                }
            case TiffTag.IccProfile:
                {
                    directory.IccProfile = reader.Slice(entry.ValueOffset, entry.Count).ToArray();
                    break;
                }
            case TiffTag.JpegProc:
                {
                    GetOldJpeg(directory).Process = ReadInt32(reader, entry);
                    break;
                }
            case TiffTag.JpegRestartInterval:
                {
                    GetOldJpeg(directory).RestartInterval = ReadInt32(reader, entry);
                    break;
                }
        }
    }

    private static TiffOldJpegParameters GetOldJpeg(TiffDirectory directory)
    {
        if (directory.OldJpeg == null)
        {
            directory.OldJpeg = new TiffOldJpegParameters();
        }

        return directory.OldJpeg;
    }

    private static byte[][] ReadOldJpegQuantizationTables(in TiffSpanReader reader, long[] offsets)
    {
        var tables = new byte[offsets.Length][];
        for (int index = 0; index < offsets.Length; index++)
        {
            tables[index] = reader.Slice(offsets[index], OldJpegQuantizationTableLength).ToArray();
        }

        return tables;
    }

    private static byte[][] ReadOldJpegHuffmanTables(in TiffSpanReader reader, long[] offsets)
    {
        var tables = new byte[offsets.Length][];
        for (int index = 0; index < offsets.Length; index++)
        {
            ReadOnlySpan<byte> codeCounts = reader.Slice(offsets[index], OldJpegHuffmanCountsLength);
            int valueCount = 0;
            for (int length = 0; length < codeCounts.Length; length++)
            {
                valueCount += codeCounts[length];
            }

            tables[index] = reader.Slice(offsets[index], OldJpegHuffmanCountsLength + valueCount).ToArray();
        }

        return tables;
    }
}
