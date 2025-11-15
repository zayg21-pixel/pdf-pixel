using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfReader.Fonts.Cff;

/// <summary>
/// Low-level OpenType writing helpers (big-endian writers, checksum, alignment, numeric clamps, directory params).
/// Separated from CffOpenTypeWrapper for clarity and potential reuse.
/// </summary>
internal static class CffOpenTypeWriter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16BE(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt16BE(BinaryWriter writer, short value)
    {
        WriteUInt16BE(writer, (ushort)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32BE(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt64BE(BinaryWriter writer, long value)
    {
        WriteUInt32BE(writer, (uint)(value >> 32));
        WriteUInt32BE(writer, (uint)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32BE(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Align4(int value)
    {
        return (value + 3) & ~3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CalcTableChecksum(byte[] data)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeDirParams(ushort numTables, out ushort searchRange, out ushort entrySelector, out ushort rangeShift)
    {
        ushort maxPowerOfTwo = 1;
        entrySelector = 0;
        while ((maxPowerOfTwo << 1) <= numTables)
        {
            maxPowerOfTwo <<= 1;
            entrySelector++;
        }
        searchRange = (ushort)(maxPowerOfTwo * 16);
        rangeShift = (ushort)(numTables * 16 - searchRange);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ClampToShort(float value, short fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return fallback;
        }
        int intValue = (int)Math.Round(value);
        if (intValue > short.MaxValue)
        {
            return short.MaxValue;
        }
        if (intValue < short.MinValue)
        {
            return short.MinValue;
        }
        return (short)intValue;
    }
}
