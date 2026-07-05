using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption;

/// <summary>
/// Pure managed SHA-384/SHA-512 implementation based on FIPS 180-4.
/// Used in place of <see cref="System.Security.Cryptography.SHA512"/> to support
/// platforms where the native implementation is unavailable (e.g., Blazor WASM).
/// Only needed by the R6 hardened-hash algorithm (ISO 32000-2 Algorithm 2.B).
/// </summary>
internal static class ManagedSha512
{
    private static readonly ulong[] RoundConstants = [
        0x428a2f98d728ae22, 0x7137449123ef65cd, 0xb5c0fbcfec4d3b2f, 0xe9b5dba58189dbbc,
        0x3956c25bf348b538, 0x59f111f1b605d019, 0x923f82a4af194f9b, 0xab1c5ed5da6d8118,
        0xd807aa98a3030242, 0x12835b0145706fbe, 0x243185be4ee4b28c, 0x550c7dc3d5ffb4e2,
        0x72be5d74f27b896f, 0x80deb1fe3b1696b1, 0x9bdc06a725c71235, 0xc19bf174cf692694,
        0xe49b69c19ef14ad2, 0xefbe4786384f25e3, 0x0fc19dc68b8cd5b5, 0x240ca1cc77ac9c65,
        0x2de92c6f592b0275, 0x4a7484aa6ea6e483, 0x5cb0a9dcbd41fbd4, 0x76f988da831153b5,
        0x983e5152ee66dfab, 0xa831c66d2db43210, 0xb00327c898fb213f, 0xbf597fc7beef0ee4,
        0xc6e00bf33da88fc2, 0xd5a79147930aa725, 0x06ca6351e003826f, 0x142929670a0e6e70,
        0x27b70a8546d22ffc, 0x2e1b21385c26c926, 0x4d2c6dfc5ac42aed, 0x53380d139d95b3df,
        0x650a73548baf63de, 0x766a0abb3c77b2a8, 0x81c2c92e47edaee6, 0x92722c851482353b,
        0xa2bfe8a14cf10364, 0xa81a664bbc423001, 0xc24b8b70d0f89791, 0xc76c51a30654be30,
        0xd192e819d6ef5218, 0xd69906245565a910, 0xf40e35855771202a, 0x106aa07032bbd1b8,
        0x19a4c116b8d2d0c8, 0x1e376c085141ab53, 0x2748774cdf8eeb99, 0x34b0bcb5e19b48a8,
        0x391c0cb3c5c95a63, 0x4ed8aa4ae3418acb, 0x5b9cca4f7763e373, 0x682e6ff3d6b2b8a3,
        0x748f82ee5defb2fc, 0x78a5636f43172f60, 0x84c87814a1f0ab72, 0x8cc702081a6439ec,
        0x90befffa23631e28, 0xa4506cebde82bde9, 0xbef9a3f7b2c67915, 0xc67178f2e372532b,
        0xca273eceea26619c, 0xd186b8c721c0c207, 0xeada7dd6cde0eb1e, 0xf57d4f7fee6ed178,
        0x06f067aa72176fba, 0x0a637dc5a2c898a6, 0x113f9804bef90dae, 0x1b710b35131c471b,
        0x28db77f523047d84, 0x32caab7b40c72493, 0x3c9ebe0a15c9bebc, 0x431d67c49c100d4c,
        0x4cc5d4becb3e42b6, 0x597f299cfc657e2a, 0x5fcb6fab3ad6faec, 0x6c44198c4a475817
    ];

    private static readonly ulong[] Sha512InitialHash = [
        0x6a09e667f3bcc908, 0xbb67ae8584caa73b, 0x3c6ef372fe94f82b, 0xa54ff53a5f1d36f1,
        0x510e527fade682d1, 0x9b05688c2b3e6c1f, 0x1f83d9abfb41bd6b, 0x5be0cd19137e2179
    ];

    private static readonly ulong[] Sha384InitialHash = [
        0xcbbb9d5dc1059ed8, 0x629a292a367cd507, 0x9159015a3070dd17, 0x152fecd8f70e5939,
        0x67332667ffc00b31, 0x8eb44a8768581511, 0xdb0c2e0d64f98fa7, 0x47b5481dbefa4fa4
    ];

    /// <summary>
    /// Computes the SHA-512 hash of <paramref name="data"/>, returning a 64-byte digest.
    /// </summary>
    public static byte[] ComputeHash512(byte[] data) => ComputeHash(data, Sha512InitialHash, 64);

    /// <summary>
    /// Computes the SHA-384 hash of <paramref name="data"/>, returning a 48-byte digest.
    /// </summary>
    public static byte[] ComputeHash384(byte[] data) => ComputeHash(data, Sha384InitialHash, 48);

    private static byte[] ComputeHash(byte[] data, ulong[] initialHash, int outputLength)
    {
        var h = new ulong[8];
        Array.Copy(initialHash, h, 8);

        byte[] padded = Pad(data);
        var w = new ulong[80];

        for (int blockStart = 0; blockStart < padded.Length; blockStart += 128)
        {
            for (int i = 0; i < 16; i++)
            {
                int offset = blockStart + (i * 8);
                w[i] = ReadUInt64Be(padded, offset);
            }

            for (int i = 16; i < 80; i++)
            {
                ulong s0 = RightRotate(w[i - 15], 1) ^ RightRotate(w[i - 15], 8) ^ (w[i - 15] >> 7);
                ulong s1 = RightRotate(w[i - 2], 19) ^ RightRotate(w[i - 2], 61) ^ (w[i - 2] >> 6);
                w[i] = w[i - 16] + s0 + w[i - 7] + s1;
            }

            ulong a = h[0];
            ulong b = h[1];
            ulong c = h[2];
            ulong d = h[3];
            ulong e = h[4];
            ulong f = h[5];
            ulong g = h[6];
            ulong hh = h[7];

            for (int i = 0; i < 80; i++)
            {
                ulong bigSigma1 = RightRotate(e, 14) ^ RightRotate(e, 18) ^ RightRotate(e, 41);
                ulong ch = (e & f) ^ (~e & g);
                ulong temp1 = hh + bigSigma1 + ch + RoundConstants[i] + w[i];
                ulong bigSigma0 = RightRotate(a, 28) ^ RightRotate(a, 34) ^ RightRotate(a, 39);
                ulong maj = (a & b) ^ (a & c) ^ (b & c);
                ulong temp2 = bigSigma0 + maj;

                hh = g;
                g = f;
                f = e;
                e = d + temp1;
                d = c;
                c = b;
                b = a;
                a = temp1 + temp2;
            }

            h[0] += a;
            h[1] += b;
            h[2] += c;
            h[3] += d;
            h[4] += e;
            h[5] += f;
            h[6] += g;
            h[7] += hh;
        }

        var hash = new byte[64];
        for (int i = 0; i < 8; i++)
        {
            WriteUInt64Be(hash, i * 8, h[i]);
        }

        if (outputLength == 64)
        {
            return hash;
        }

        var truncated = new byte[outputLength];
        Buffer.BlockCopy(hash, 0, truncated, 0, outputLength);
        return truncated;
    }

    private static byte[] Pad(byte[] data)
    {
        // SHA-512 uses a 128-bit big-endian bit-length suffix and 128-byte blocks.
        int paddedLength = data.Length + 1 + 16;
        paddedLength += (128 - (paddedLength % 128)) % 128;

        var padded = new byte[paddedLength];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        padded[data.Length] = 0x80;

        ulong bitLength = (ulong)data.Length * 8;
        for (int i = 0; i < 8; i++)
        {
            padded[paddedLength - 1 - i] = (byte)(bitLength >> (i * 8));
        }

        return padded;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RightRotate(ulong value, int amount) => (value >> amount) | (value << (64 - amount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64Be(byte[] buffer, int offset)
    {
        ulong value = 0;
        for (int i = 0; i < 8; i++)
        {
            value = (value << 8) | buffer[offset + i];
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt64Be(byte[] buffer, int offset, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            buffer[offset + i] = (byte)(value >> ((7 - i) * 8));
        }
    }
}
