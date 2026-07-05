using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption;

/// <summary>
/// Pure managed SHA-256 implementation based on FIPS 180-4.
/// Used in place of <see cref="System.Security.Cryptography.SHA256"/> to support
/// platforms where the native implementation is unavailable (e.g., Blazor WASM).
/// </summary>
internal static class ManagedSha256
{
    private static readonly uint[] RoundConstants = [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    ];

    /// <summary>
    /// Computes the SHA-256 hash of <paramref name="data"/>, returning a 32-byte digest.
    /// </summary>
    public static byte[] ComputeHash(byte[] data)
    {
        uint h0 = 0x6a09e667;
        uint h1 = 0xbb67ae85;
        uint h2 = 0x3c6ef372;
        uint h3 = 0xa54ff53a;
        uint h4 = 0x510e527f;
        uint h5 = 0x9b05688c;
        uint h6 = 0x1f83d9ab;
        uint h7 = 0x5be0cd19;

        byte[] padded = Pad(data);
        var w = new uint[64];

        for (int blockStart = 0; blockStart < padded.Length; blockStart += 64)
        {
            for (int i = 0; i < 16; i++)
            {
                int offset = blockStart + (i * 4);
                w[i] = (uint)((padded[offset] << 24) | (padded[offset + 1] << 16) | (padded[offset + 2] << 8) | padded[offset + 3]);
            }

            for (int i = 16; i < 64; i++)
            {
                uint s0 = RightRotate(w[i - 15], 7) ^ RightRotate(w[i - 15], 18) ^ (w[i - 15] >> 3);
                uint s1 = RightRotate(w[i - 2], 17) ^ RightRotate(w[i - 2], 19) ^ (w[i - 2] >> 10);
                w[i] = w[i - 16] + s0 + w[i - 7] + s1;
            }

            uint a = h0;
            uint b = h1;
            uint c = h2;
            uint d = h3;
            uint e = h4;
            uint f = h5;
            uint g = h6;
            uint h = h7;

            for (int i = 0; i < 64; i++)
            {
                uint bigSigma1 = RightRotate(e, 6) ^ RightRotate(e, 11) ^ RightRotate(e, 25);
                uint ch = (e & f) ^ (~e & g);
                uint temp1 = h + bigSigma1 + ch + RoundConstants[i] + w[i];
                uint bigSigma0 = RightRotate(a, 2) ^ RightRotate(a, 13) ^ RightRotate(a, 22);
                uint maj = (a & b) ^ (a & c) ^ (b & c);
                uint temp2 = bigSigma0 + maj;

                h = g;
                g = f;
                f = e;
                e = d + temp1;
                d = c;
                c = b;
                b = a;
                a = temp1 + temp2;
            }

            h0 += a;
            h1 += b;
            h2 += c;
            h3 += d;
            h4 += e;
            h5 += f;
            h6 += g;
            h7 += h;
        }

        var hash = new byte[32];
        WriteUInt32Be(hash, 0, h0);
        WriteUInt32Be(hash, 4, h1);
        WriteUInt32Be(hash, 8, h2);
        WriteUInt32Be(hash, 12, h3);
        WriteUInt32Be(hash, 16, h4);
        WriteUInt32Be(hash, 20, h5);
        WriteUInt32Be(hash, 24, h6);
        WriteUInt32Be(hash, 28, h7);
        return hash;
    }

    private static byte[] Pad(byte[] data)
    {
        long bitLength = (long)data.Length * 8;
        int paddedLength = data.Length + 1 + 8;
        paddedLength += (64 - (paddedLength % 64)) % 64;

        var padded = new byte[paddedLength];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        padded[data.Length] = 0x80;

        for (int i = 0; i < 8; i++)
        {
            padded[paddedLength - 1 - i] = (byte)(bitLength >> (i * 8));
        }

        return padded;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RightRotate(uint value, int amount) => (value >> amount) | (value << (32 - amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset + 0] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
