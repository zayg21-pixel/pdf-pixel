using PdfPixel.Ccitt;
using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using PdfPixel.Tiff.Model;
using PdfPixel.Tiff.Streams;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfPixel.Tiff.Decoding;

/// <summary>
/// Decompresses one strip or tile of a TIFF image into unpredicted, big-endian, MSB-first packed samples.
/// </summary>
internal sealed class TiffChunkDecoder
{
    private const int ZlibHeaderLength = 2;
    private const int JpegMarkerLength = 2;

    private readonly TiffDirectory _directory;

    private TiffOldJpegStreamBuilder? _oldJpegStreamBuilder;
    private TiffYCbCrConverter? _yCbCrConverter;

    public TiffChunkDecoder(TiffDirectory directory) => _directory = directory;

    /// <summary>
    /// Decodes a chunk into <paramref name="destination"/>, which holds <paramref name="chunkRows"/> rows of
    /// <paramref name="chunkWidth"/> pixels made of samples <paramref name="sampleBits"/> bits deep.
    /// </summary>
    public void Decode(in ReadOnlyMemory<byte> chunk, int chunkWidth, int chunkRows, int[] sampleBits, byte[] destination)
    {
        int bitsPerPixel = 0;
        for (int sample = 0; sample < sampleBits.Length; sample++)
        {
            bitsPerPixel += sampleBits[sample];
        }

        int rowBytes = ((chunkWidth * bitsPerPixel) + 7) / 8;
        ReadOnlyMemory<byte> encoded = (_directory.FillOrder == TiffFillOrder.LeastSignificantBitFirst)
            ? ReverseBitOrder(chunk.Span)
            : chunk;

        switch (_directory.Compression)
        {
            case TiffCompression.Uncompressed:
                {
                    CopyUncompressed(encoded.Span, destination);
                    break;
                }
            case TiffCompression.Lzw:
                {
                    ReadFully(new LzwDecodeStream(CreateMemoryStream(encoded), leaveOpen: false, earlyChange: true), destination);
                    break;
                }
            case TiffCompression.AdobeDeflate:
            case TiffCompression.Deflate:
                {
                    ReadFully(new FlateDecodeStream(CreateMemoryStream(encoded.Slice(Math.Min(ZlibHeaderLength, encoded.Length)))), destination);
                    break;
                }
            case TiffCompression.PackBits:
                {
                    ReadFully(new RunLengthDecodeStream(CreateMemoryStream(encoded), leaveOpen: false, endsAtNoOperation: false), destination);
                    break;
                }
            case TiffCompression.CcittRle:
            case TiffCompression.CcittGroup3:
            case TiffCompression.CcittGroup4:
                {
                    DecodeCcitt(encoded, chunkWidth, chunkRows, rowBytes, destination);
                    return;
                }
            case TiffCompression.Jpeg:
                {
                    DecodeJpeg(encoded, chunkWidth, chunkRows, sampleBits.Length, rowBytes, destination);
                    return;
                }
            case TiffCompression.OldJpeg:
                {
                    DecodeOldJpeg(encoded.Span, chunkWidth, chunkRows, sampleBits.Length, rowBytes, destination);
                    return;
                }
            case TiffCompression.Jpeg2000:
            case TiffCompression.AperioJpeg2000Rgb:
                {
                    DecodeJpeg2000(encoded.Span, chunkWidth, chunkRows, sampleBits, rowBytes, destination);
                    return;
                }
            default:
                {
                    // TODO: [LOW] Aperio YCbCr JPEG 2000 (compression 33003). Its codestreams subsample chroma, which
                    // PdfPixel.Jpx does not decode yet, and no redistributable sample is available to test against.
                    throw new NotSupportedException($"TIFF compression {_directory.Compression} is not supported.");
                }
        }

        if (_directory.Predictor == TiffPredictor.FloatingPoint)
        {
            UndoFloatingPointPredictor(chunkWidth, chunkRows, sampleBits, rowBytes, destination);
            return;
        }

        if (_directory.IsLittleEndian)
        {
            SwapSampleBytes(sampleBits, chunkWidth, chunkRows, rowBytes, destination);
        }

        if (_directory.Predictor == TiffPredictor.Horizontal)
        {
            UndoHorizontalPredictor(chunkWidth, chunkRows, sampleBits, rowBytes, destination);
        }
    }

    private void DecodeCcitt(in ReadOnlyMemory<byte> encoded, int chunkWidth, int chunkRows, int rowBytes, byte[] destination)
    {
        int k;
        bool byteAlign;

        switch (_directory.Compression)
        {
            case TiffCompression.CcittRle:
                {
                    k = 0;
                    byteAlign = true;
                    break;
                }
            case TiffCompression.CcittGroup3:
                {
                    k = ((_directory.T4Options & 1) != 0) ? 1 : 0;
                    byteAlign = false;
                    break;
                }
            default:
                {
                    k = -1;
                    byteAlign = false;
                    break;
                }
        }

        CcittRowDecoder rowDecoder = new(encoded, chunkWidth, chunkRows, blackIs1: true, k, endOfLine: false, byteAlign, endOfBlock: false);

        for (int row = 0; row < chunkRows; row++)
        {
            if (!rowDecoder.DecodeNextRow(destination.AsSpan(row * rowBytes, rowBytes)))
            {
                return;
            }
        }
    }

    private void DecodeJpeg(in ReadOnlyMemory<byte> encoded, int chunkWidth, int chunkRows, int samplesPerPixel, int rowBytes, byte[] destination)
    {
        ReadOnlyMemory<byte> jpegData = PrependJpegTables(_directory.JpegTables, encoded.Span);
        JpgYuvMode yuvMode = (_directory.Photometric == TiffPhotometric.YCbCr) ? JpgYuvMode.ForceYuv : JpgYuvMode.NoYuv;

        DecodeJpegStream(jpegData, chunkWidth, chunkRows, samplesPerPixel, rowBytes, yuvMode, destination);
    }

    private void DecodeOldJpeg(in ReadOnlySpan<byte> encoded, int chunkWidth, int chunkRows, int samplesPerPixel, int rowBytes, byte[] destination)
    {
        if (_oldJpegStreamBuilder == null)
        {
            _oldJpegStreamBuilder = new TiffOldJpegStreamBuilder(_directory);
        }

        byte[] jpegData = _oldJpegStreamBuilder.Build(encoded, chunkWidth, chunkRows, samplesPerPixel);
        int rows = DecodeJpegStream(jpegData, chunkWidth, chunkRows, samplesPerPixel, rowBytes, JpgYuvMode.NoYuv, destination);

        if (_directory.Photometric != TiffPhotometric.YCbCr)
        {
            return;
        }

        if (_yCbCrConverter == null)
        {
            _yCbCrConverter = new TiffYCbCrConverter(_directory);
        }

        for (int row = 0; row < rows; row++)
        {
            for (int pixel = 0; pixel < chunkWidth; pixel++)
            {
                Span<byte> samples = destination.AsSpan((row * rowBytes) + (pixel * 3), 3);
                _yCbCrConverter.Convert(samples[0], samples[1], samples[2], samples);
            }
        }
    }

    /// <summary>
    /// Decodes a complete JPEG stream into <paramref name="destination"/> and returns the number of rows written.
    /// </summary>
    private static int DecodeJpegStream(
        in ReadOnlyMemory<byte> jpegData,
        int chunkWidth,
        int chunkRows,
        int samplesPerPixel,
        int rowBytes,
        JpgYuvMode yuvMode,
        byte[] destination)
    {
        JpgHeader header = JpgReader.ParseHeader(jpegData.Span);

        if (header.ContentOffset < 0 || header.Width != chunkWidth || header.ComponentCount != samplesPerPixel)
        {
            throw new InvalidDataException(
                $"TIFF JPEG chunk is {header.Width} pixels wide with {header.ComponentCount} component(s), "
                    + $"expected {chunkWidth} with {samplesPerPixel}.");
        }

        JpgDecoderOptions options = new()
        {
            YuvMode = yuvMode,
            InvertCmykColors = false
        };

        IJpgDecoder rowDecoder = JpgDecoderFactory.Create(header, jpegData, options);
        int rows = Math.Min(header.Height, chunkRows);

        for (int row = 0; row < rows; row++)
        {
            if (!rowDecoder.TryReadRow(destination.AsSpan(row * rowBytes, rowBytes)))
            {
                return row;
            }
        }

        return rows;
    }

    private static void DecodeJpeg2000(in ReadOnlySpan<byte> encoded, int chunkWidth, int chunkRows, int[] sampleBits, int rowBytes, byte[] destination)
    {
        JpxHeader header = JpxReader.ParseHeader(encoded);
        JpxTileProvider tileProvider = new(header, encoded.Slice(header.CodestreamOffset));
        JpxTileToRowConverter rowConverter = new(header, tileProvider);

        if (rowConverter.Width != chunkWidth
            || rowConverter.ComponentCount != sampleBits.Length
            || rowConverter.BitsPerComponent != sampleBits[0])
        {
            throw new InvalidDataException(
                $"TIFF JPEG 2000 chunk is {rowConverter.Width} pixels wide with {rowConverter.ComponentCount} "
                    + $"{rowConverter.BitsPerComponent}-bit component(s), expected {chunkWidth} with {sampleBits.Length} {sampleBits[0]}-bit.");
        }

        int rows = Math.Min(rowConverter.Height, chunkRows);

        for (int row = 0; row < rows; row++)
        {
            if (!rowConverter.TryGetNextRow(destination.AsSpan(row * rowBytes, rowBytes)))
            {
                return;
            }
        }
    }

    private static void UndoHorizontalPredictor(int chunkWidth, int chunkRows, int[] sampleBits, int rowBytes, byte[] destination)
    {
        int bitsPerSample = sampleBits[0];
        int bytesPerSample = Math.Max(1, bitsPerSample / 8);
        var row = new byte[rowBytes];

        for (int rowIndex = 0; rowIndex < chunkRows; rowIndex++)
        {
            int rowOffset = rowIndex * rowBytes;
            Buffer.BlockCopy(destination, rowOffset, row, 0, rowBytes);
            TiffPredictorUndo.UndoTiffPredictor(row, chunkWidth, sampleBits.Length, bitsPerSample, bytesPerSample);
            Buffer.BlockCopy(row, 0, destination, rowOffset, rowBytes);
        }
    }

    private static void UndoFloatingPointPredictor(int chunkWidth, int chunkRows, int[] sampleBits, int rowBytes, byte[] destination)
    {
        var planes = new byte[rowBytes];

        for (int rowIndex = 0; rowIndex < chunkRows; rowIndex++)
        {
            TiffPredictorUndo.UndoFloatingPointPredictor(destination.AsSpan(rowIndex * rowBytes, rowBytes), chunkWidth, sampleBits.Length, sampleBits[0] / 8, planes);
        }
    }

    private static void CopyUncompressed(in ReadOnlySpan<byte> encoded, byte[] destination)
    {
        int length = Math.Min(encoded.Length, destination.Length);
        encoded.Slice(0, length).CopyTo(destination);
    }

    private static void ReadFully(Stream stream, byte[] destination)
    {
        using (stream)
        {
            int total = 0;
            while (total < destination.Length)
            {
                int read = stream.Read(destination, total, destination.Length - total);
                if (read <= 0)
                {
                    return;
                }

                total += read;
            }
        }
    }

    private static MemoryStream CreateMemoryStream(in ReadOnlyMemory<byte> encoded)
    {
        if (MemoryMarshal.TryGetArray(encoded, out ArraySegment<byte> segment) && segment.Array != null)
        {
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false);
        }

        return new MemoryStream(encoded.ToArray(), writable: false);
    }

    private static ReadOnlyMemory<byte> PrependJpegTables(byte[]? jpegTables, in ReadOnlySpan<byte> chunk)
    {
        if (jpegTables == null || jpegTables.Length < 2 * JpegMarkerLength || chunk.Length < JpegMarkerLength)
        {
            return chunk.ToArray();
        }

        // Tables stream: SOI, tables, EOI. Chunk: SOI, frame, scan, EOI. The EOI and SOI in between are dropped.
        int tablesLength = jpegTables.Length - JpegMarkerLength;
        var combined = new byte[tablesLength + chunk.Length - JpegMarkerLength];
        Buffer.BlockCopy(jpegTables, 0, combined, 0, tablesLength);
        chunk.Slice(JpegMarkerLength).CopyTo(combined.AsSpan(tablesLength));

        return combined;
    }

    private static byte[] ReverseBitOrder(in ReadOnlySpan<byte> encoded)
    {
        var reversed = new byte[encoded.Length];
        for (int index = 0; index < encoded.Length; index++)
        {
            int value = encoded[index];
            int result = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                result = (result << 1) | ((value >> bit) & 1);
            }

            reversed[index] = (byte)result;
        }

        return reversed;
    }

    private static void SwapSampleBytes(int[] sampleBits, int chunkWidth, int chunkRows, int rowBytes, byte[] destination)
    {
        var hasMultiByteSample = false;
        for (int sample = 0; sample < sampleBits.Length; sample++)
        {
            if (sampleBits[sample] % 8 != 0)
            {
                return;
            }

            hasMultiByteSample |= sampleBits[sample] > 8;
        }

        if (!hasMultiByteSample)
        {
            return;
        }

        for (int row = 0; row < chunkRows; row++)
        {
            int offset = row * rowBytes;
            for (int pixel = 0; pixel < chunkWidth; pixel++)
            {
                for (int sample = 0; sample < sampleBits.Length; sample++)
                {
                    int sampleBytes = sampleBits[sample] / 8;
                    Array.Reverse(destination, offset, sampleBytes);
                    offset += sampleBytes;
                }
            }
        }
    }
}
