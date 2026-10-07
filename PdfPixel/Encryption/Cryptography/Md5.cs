using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption.Cryptography;

/// <summary>
/// Pure managed MD5 implementation based on RFC 1321.
/// Used in place of <see cref="System.Security.Cryptography.MD5"/> to support
/// platforms where the native implementation is unavailable (e.g., Blazor WASM).
/// </summary>
internal static class Md5
{
    /// <summary>
    /// Per-round shift amounts (RFC 1321, section 3.4).
    /// </summary>
    private static readonly int[] ShiftAmounts = [
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21
    ];

    /// <summary>
    /// Pre-computed sine-derived constants (RFC 1321, section 3.4).
    /// </summary>
    private static readonly uint[] SineTable = [
        0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee, 0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
        0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be, 0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
        0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa, 0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
        0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed, 0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
        0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c, 0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
        0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05, 0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
        0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039, 0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
        0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1, 0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391
    ];

    /// <summary>
    /// Computes the MD5 hash of <paramref name="data"/>, returning a 16-byte digest.
    /// </summary>
    public static byte[] ComputeHash(in ReadOnlySpan<byte> data)
    {
        uint h0 = 0x67452301;
        uint h1 = 0xefcdab89;
        uint h2 = 0x98badcfe;
        uint h3 = 0x10325476;

        byte[] padded = Pad(data);
        Span<uint> messageWords = stackalloc uint[16];

        for (int blockStart = 0; blockStart < padded.Length; blockStart += 64)
        {
            for (int i = 0; i < 16; i++)
            {
                int offset = blockStart + (i * 4);
                messageWords[i] = (uint)(padded[offset] | (padded[offset + 1] << 8) | (padded[offset + 2] << 16) | (padded[offset + 3] << 24));
            }

            uint a = h0;
            uint b = h1;
            uint c = h2;
            uint d = h3;

            for (int i = 0; i < 64; i++)
            {
                uint f;
                int g;

                if (i < 16)
                {
                    f = (b & c) | (~b & d);
                    g = i;
                }
                else if (i < 32)
                {
                    f = (d & b) | (~d & c);
                    g = ((5 * i) + 1) % 16;
                }
                else if (i < 48)
                {
                    f = b ^ c ^ d;
                    g = ((3 * i) + 5) % 16;
                }
                else
                {
                    f = c ^ (b | ~d);
                    g = (7 * i) % 16;
                }

                uint temp = d;
                d = c;
                c = b;
                b += LeftRotate(a + f + SineTable[i] + messageWords[g], ShiftAmounts[i]);
                a = temp;
            }

            h0 += a;
            h1 += b;
            h2 += c;
            h3 += d;
        }

        var hash = new byte[16];
        WriteUInt32Le(hash, 0, h0);
        WriteUInt32Le(hash, 4, h1);
        WriteUInt32Le(hash, 8, h2);
        WriteUInt32Le(hash, 12, h3);
        return hash;
    }

    private static byte[] Pad(in ReadOnlySpan<byte> data)
    {
        long bitLength = (long)data.Length * 8;
        int paddedLength = data.Length + 1 + 8;
        paddedLength += (64 - (paddedLength % 64)) % 64;

        var padded = new byte[paddedLength];
        data.CopyTo(padded);
        padded[data.Length] = 0x80;

        for (int i = 0; i < 8; i++)
        {
            padded[paddedLength - 8 + i] = (byte)(bitLength >> (i * 8));
        }

        return padded;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LeftRotate(uint value, int amount) => (value << amount) | (value >> (32 - amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt32Le(byte[] buffer, int offset, uint value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
