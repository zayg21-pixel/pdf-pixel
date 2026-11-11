using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Numerics;
#endif

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Reads and writes single samples of a row of packed samples by their index, most significant bit first, for steps
/// that walk a row from its end, and counts the set samples of a row of 1-bit samples.
/// </summary>
public static class PdfPackedSamples
{
    private const int BitsPerByte = 8;
    private const int ByteMask = 0xFF;

    /// <summary>
    /// Returns the number of set bits from bit <paramref name="start"/> up to, not including, bit
    /// <paramref name="end"/> of the row at <paramref name="row"/>, most significant bit first.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountSetBits(ref byte row, int start, int end)
    {
        int firstByte = start >> 3;
        int lastByte = (end - 1) >> 3;
        int firstMask = ByteMask >> (start & 7);
        int lastMask = (ByteMask << (BitsPerByte - 1 - ((end - 1) & 7))) & ByteMask;
        int count;

        if (firstByte == lastByte)
        {
            count = PopCount((ulong)(Unsafe.Add(ref row, firstByte) & firstMask & lastMask));
        }
        else
        {
            count = PopCount((ulong)(Unsafe.Add(ref row, firstByte) & firstMask)) + PopCount((ulong)(Unsafe.Add(ref row, lastByte) & lastMask));
            int index = firstByte + 1;

            for (; index + sizeof(ulong) <= lastByte; index += sizeof(ulong))
            {
                count += PopCount(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, index)));
            }

            for (; index < lastByte; index++)
            {
                count += PopCount(Unsafe.Add(ref row, index));
            }
        }

        return count;
    }

    /// <summary>
    /// Returns sample <paramref name="sampleIndex"/> of the row at <paramref name="row"/>, of
    /// <paramref name="bitsPerComponent"/> bits, 1 to 16.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Read(ref byte row, int sampleIndex, int bitsPerComponent)
    {
        long bitOffset = (long)sampleIndex * bitsPerComponent;
        var byteIndex = (int)(bitOffset >> 3);
        int bitCount = (int)(bitOffset & 7) + bitsPerComponent;
        int byteCount = (bitCount + BitsPerByte - 1) / BitsPerByte;
        uint value = 0;

        for (int index = 0; index < byteCount; index++)
        {
            value = (value << BitsPerByte) | Unsafe.Add(ref row, byteIndex + index);
        }

        return (value >> ((byteCount * BitsPerByte) - bitCount)) & ((1u << bitsPerComponent) - 1u);
    }

    /// <summary>
    /// Writes <paramref name="code"/> as sample <paramref name="sampleIndex"/> of the row at <paramref name="row"/>,
    /// of <paramref name="bitsPerComponent"/> bits: 1, 2, 4, 8 or 16, so that no sample spans two bytes but at 16.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte row, int sampleIndex, int bitsPerComponent, uint code)
    {
        switch (bitsPerComponent)
        {
            case 16:
            {
                ref byte first = ref Unsafe.Add(ref row, sampleIndex * 2);
                first = (byte)(code >> BitsPerByte);
                Unsafe.Add(ref first, 1) = (byte)code;
                break;
            }
            case 8:
            {
                Unsafe.Add(ref row, sampleIndex) = (byte)code;
                break;
            }
            default:
            {
                int bitOffset = sampleIndex * bitsPerComponent;
                ref byte target = ref Unsafe.Add(ref row, bitOffset >> 3);
                int shift = BitsPerByte - bitsPerComponent - (bitOffset & 7);
                var mask = (byte)(((1 << bitsPerComponent) - 1) << shift);
                target = (byte)((target & ~mask) | ((int)(code << shift) & mask));
                break;
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount(ulong value)
    {
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;

        return (int)((value * 0x0101010101010101UL) >> 56);
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount(ulong value) => BitOperations.PopCount(value);
#endif
}
