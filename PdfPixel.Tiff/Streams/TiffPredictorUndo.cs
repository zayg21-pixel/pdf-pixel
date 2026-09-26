using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Tiff.Streams;

/// <summary>
/// Undoes the TIFF horizontal differencing predictor (Predictor 2).
/// </summary>
public static class TiffPredictorUndo
{
    /// <summary>
    /// Undoes the predictor in place, replacing each sample with sample + left.
    /// </summary>
    /// <param name="row">Row buffer containing encoded (predicted) samples. Modified in place to decoded form.</param>
    /// <param name="columns">Number of pixel columns in the image row.</param>
    /// <param name="colors">Number of color components per pixel (samples per pixel).</param>
    /// <param name="bitsPerComponent">Bits per component (1,2,4,8,16,32 supported).</param>
    /// <param name="bytesPerSample">Bytes per sample (1 for &lt;=8 bpc, 2 for 16 bpc, 4 for 32 bpc). For packed sub-byte samples this remains 1.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UndoTiffPredictor(byte[] row, int columns, int colors, int bitsPerComponent, int bytesPerSample)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        if (colors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(colors));
        }

        if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16 && bitsPerComponent != 32)
        {
            throw new NotSupportedException("Unsupported bitsPerComponent for TIFF predictor undo.");
        }

        int samplesPerRow = columns * colors;

        // Byte-aligned samples (8, 16 or 32 bpc).
        if (bitsPerComponent >= 8)
        {
            if (bytesPerSample == 1)
            {
                for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                {
                    int leftIndex = sampleIndex - colors;
                    int left = (leftIndex >= 0) ? row[leftIndex] : 0;
                    int current = row[sampleIndex];
                    row[sampleIndex] = (byte)((current + left) & 0xFF);
                }
            }
            else if (bytesPerSample == 4)
            {
                // 32-bit samples: big-endian.
                Span<byte> rowSpan = row;
                for (int sampleIndex = colors; sampleIndex < samplesPerRow; sampleIndex++)
                {
                    Span<byte> current = rowSpan.Slice(sampleIndex * 4, 4);
                    uint left = BinaryPrimitives.ReadUInt32BigEndian(rowSpan.Slice((sampleIndex - colors) * 4, 4));
                    BinaryPrimitives.WriteUInt32BigEndian(current, BinaryPrimitives.ReadUInt32BigEndian(current) + left);
                }
            }
            else
            {
                // 16-bit samples: big-endian.
                for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                {
                    int byteIndex = sampleIndex * 2;
                    int current = (row[byteIndex] << 8) | row[byteIndex + 1];
                    int left = 0;
                    if (sampleIndex >= colors)
                    {
                        int leftByteIndex = (sampleIndex - colors) * 2;
                        left = (row[leftByteIndex] << 8) | row[leftByteIndex + 1];
                    }

                    int decoded = (current + left) & 0xFFFF;
                    row[byteIndex] = (byte)(decoded >> 8);
                    row[byteIndex + 1] = (byte)(decoded & 0xFF);
                }
            }

            return;
        }

        // Packed (sub-byte) samples: unpack, apply the predictor, repack.
        int bits = bitsPerComponent;
        int sampleMask = (1 << bits) - 1;
        var samples = new int[samplesPerRow];
        int bitPos = 0;

        for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
        {
            int byteIndex = bitPos >> 3;
            int intraBits = bitPos & 7;
            int remainingBits = 8 - intraBits;
            int value;
            if (remainingBits >= bits)
            {
                int shift = remainingBits - bits;
                value = (row[byteIndex] >> shift) & sampleMask;
            }
            else
            {
                // Sample spans two bytes.
                int firstPart = row[byteIndex] & ((1 << remainingBits) - 1);
                int secondPart = row[byteIndex + 1] >> (8 - (bits - remainingBits));
                value = ((firstPart << (bits - remainingBits)) | secondPart) & sampleMask;
            }

            int leftSampleIndex = sampleIndex - colors;
            int left = (leftSampleIndex >= 0) ? samples[leftSampleIndex] : 0;
            samples[sampleIndex] = (value + left) & sampleMask;
            bitPos += bits;
        }

        Array.Clear(row, 0, row.Length);

        int outBitPos = 0;
        for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
        {
            int value = samples[sampleIndex] & sampleMask;
            int outByteIndex = outBitPos >> 3;
            int outIntra = outBitPos & 7;
            int freeBits = 8 - outIntra;
            if (freeBits >= bits)
            {
                int shift = freeBits - bits;
                int mask = sampleMask << shift;
                row[outByteIndex] = (byte)((row[outByteIndex] & ~mask) | ((value & sampleMask) << shift));
            }
            else
            {
                int firstBits = freeBits;
                int secondBits = bits - firstBits;
                int firstMask = (1 << firstBits) - 1;
                int firstValue = (value >> secondBits) & firstMask;
                row[outByteIndex] = (byte)((row[outByteIndex] & ~firstMask) | firstValue);
                int secondValue = value & ((1 << secondBits) - 1);
                int secondShift = 8 - secondBits;
                int secondMask = ((1 << secondBits) - 1) << secondShift;
                row[outByteIndex + 1] = (byte)((row[outByteIndex + 1] & ~secondMask) | (secondValue << secondShift));
            }

            outBitPos += bits;
        }
    }

    /// <summary>
    /// Undoes the floating-point predictor (Predictor 3, Adobe TIFF Technical Note 3) in place, leaving big-endian samples.
    /// </summary>
    /// <param name="row">Row holding the byte planes of its samples, most significant plane first, differenced byte by byte.</param>
    /// <param name="columns">Number of pixel columns in the image row.</param>
    /// <param name="colors">Number of color components per pixel (samples per pixel).</param>
    /// <param name="bytesPerSample">Bytes per floating-point sample (2, 3, 4 or 8).</param>
    public static void UndoFloatingPointPredictor(in Span<byte> row, int columns, int colors, int bytesPerSample)
        => UndoFloatingPointPredictor(row, columns, colors, bytesPerSample, new byte[columns * colors * bytesPerSample]);

    /// <summary>
    /// Undoes the floating-point predictor (Predictor 3, Adobe TIFF Technical Note 3) in place, leaving big-endian samples.
    /// </summary>
    /// <param name="row">Row holding the byte planes of its samples, most significant plane first, differenced byte by byte.</param>
    /// <param name="columns">Number of pixel columns in the image row.</param>
    /// <param name="colors">Number of color components per pixel (samples per pixel).</param>
    /// <param name="bytesPerSample">Bytes per floating-point sample (2, 3, 4 or 8).</param>
    /// <param name="planes">Buffer holding a copy of the byte planes, at least one row long.</param>
    internal static void UndoFloatingPointPredictor(in Span<byte> row, int columns, int colors, int bytesPerSample, byte[] planes)
    {
        int samplesPerRow = columns * colors;
        int rowBytes = samplesPerRow * bytesPerSample;

        for (int index = colors; index < rowBytes; index++)
        {
            row[index] = (byte)(row[index] + row[index - colors]);
        }

        row.Slice(0, rowBytes).CopyTo(planes);

        for (int sample = 0; sample < samplesPerRow; sample++)
        {
            for (int significance = 0; significance < bytesPerSample; significance++)
            {
                row[(sample * bytesPerSample) + significance] = planes[(significance * samplesPerRow) + sample];
            }
        }
    }
}
