using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PdfPixel.Tiff.Decoding;

public sealed partial class TiffRowDecoder
{
    /// <summary>
    /// Writes the stored samples of every plane as unsigned samples of <see cref="BitsPerComponent"/> bits.
    /// </summary>
    private void NormalizeRow(byte[][] storedRows, in Span<byte> destination)
    {
        destination.Clear();
        int targetBit = 0;

        for (int pixel = 0; pixel < Width; pixel++)
        {
            for (int sample = 0; sample < ComponentCount; sample++)
            {
                int plane = (_planeCount > 1) ? sample : 0;
                int sourceBit = (pixel * _planeBitsPerPixel[plane]) + _sampleBitOffsets[sample];

                if (_isFloat)
                {
                    double floatValue = ReadFloat(storedRows[plane], sourceBit >> 3, _sampleBits[sample]);
                    WriteBits(destination, targetBit, BitsPerComponent, ToUnitRangeSample(floatValue));
                }
                else
                {
                    uint value = ReadBits(storedRows[plane], sourceBit, _sampleBits[sample]) ^ _sampleSignBits[sample];
                    WriteBits(destination, targetBit, BitsPerComponent, (value >> _sampleRightShifts[sample]) << _sampleLeftShifts[sample]);
                }

                targetBit += BitsPerComponent;
            }
        }
    }

    /// <summary>
    /// Writes one row of RGB samples from the chunky YCbCr data units of the loaded chunks.
    /// </summary>
    private void ConvertDataUnitRow(TiffYCbCrConverter converter, int rowInChunk, in Span<byte> destination)
    {
        int lumaPerUnit = _subsamplingHorizontal * _subsamplingVertical;
        int unitBytes = lumaPerUnit + 2;
        int unitRowOffset = (rowInChunk / _subsamplingVertical) * _planeChunkRowBytes[0];
        int lumaRowOffset = (rowInChunk % _subsamplingVertical) * _subsamplingHorizontal;

        for (int column = 0; column < _chunksAcross; column++)
        {
            int left = column * _chunkWidth;
            int pixelCount = Math.Min(_chunkWidth, Width - left);
            byte[] buffer = _chunkBuffers[column];

            for (int pixel = 0; pixel < pixelCount; pixel++)
            {
                int unitOffset = unitRowOffset + ((pixel / _subsamplingHorizontal) * unitBytes);
                byte luma = buffer[unitOffset + lumaRowOffset + (pixel % _subsamplingHorizontal)];

                converter.Convert(luma, buffer[unitOffset + lumaPerUnit], buffer[unitOffset + lumaPerUnit + 1], destination.Slice((left + pixel) * 3, 3));
            }
        }
    }

    private void ConvertRowInPlace(TiffYCbCrConverter converter, in Span<byte> row)
    {
        for (int pixel = 0; pixel < Width; pixel++)
        {
            Span<byte> samples = row.Slice(pixel * 3, 3);
            converter.Convert(samples[0], samples[1], samples[2], samples);
        }
    }

    private static double ReadFloat(byte[] source, int byteOffset, int bits)
    {
        ReadOnlySpan<byte> bytes = source.AsSpan(byteOffset, bits / 8);

        switch (bits)
        {
            case 16:
                {
                    return HalfToDouble(BinaryPrimitives.ReadUInt16BigEndian(bytes));
                }
            case 32:
                {
                    int singleBits = BinaryPrimitives.ReadInt32BigEndian(bytes);
                    return Unsafe.As<int, float>(ref singleBits);
                }
            default:
                {
                    return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(bytes));
                }
        }
    }

    private static double HalfToDouble(ushort half)
    {
        int exponent = (half >> 10) & 0x1F;
        int mantissa = half & 0x3FF;
        double sign = ((half & 0x8000) != 0) ? -1 : 1;

        if (exponent == 0)
        {
            return sign * mantissa * Math.Pow(2, -24);
        }

        if (exponent == 0x1F)
        {
            return (mantissa == 0) ? sign * double.PositiveInfinity : double.NaN;
        }

        return sign * (1 + (mantissa / 1024.0)) * Math.Pow(2, exponent - 15);
    }

    /// <summary>
    /// Clamps a floating-point sample to 0 to 1 and scales it to 16 bits; NaN maps to 0.
    /// </summary>
    private static uint ToUnitRangeSample(double value)
    {
        if (!(value > 0))
        {
            return 0;
        }

        if (value >= 1)
        {
            return (uint)MaxFloatOutput;
        }

        return (uint)((value * MaxFloatOutput) + 0.5);
    }

    private static uint ReadBits(byte[] source, int bitOffset, int bitCount)
    {
        int byteIndex = bitOffset >> 3;
        int usedBits = (bitOffset & 7) + bitCount;
        int byteCount = (usedBits + 7) >> 3;

        ulong window = 0;
        for (int index = 0; index < byteCount; index++)
        {
            window = (window << 8) | source[byteIndex + index];
        }

        return (uint)((window >> ((byteCount * 8) - usedBits)) & ((1UL << bitCount) - 1));
    }

    private static void WriteBits(in Span<byte> destination, int bitOffset, int bitCount, uint value)
    {
        int byteIndex = bitOffset >> 3;
        int usedBits = (bitOffset & 7) + bitCount;
        int byteCount = (usedBits + 7) >> 3;
        ulong window = (ulong)value << ((byteCount * 8) - usedBits);

        for (int index = byteCount - 1; index >= 0; index--)
        {
            destination[byteIndex + index] |= (byte)window;
            window >>= 8;
        }
    }
}
