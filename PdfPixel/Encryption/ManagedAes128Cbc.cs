using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption;

// TODO: improve performance here and in MD5.
/// <summary>
/// Pure managed AES-128-CBC decryption with PKCS#7 unpadding.
/// Used in place of <see cref="System.Security.Cryptography.Aes"/> to support
/// platforms where the native implementation is unavailable (e.g., Blazor WASM).
/// Only decryption is implemented; encryption is not required for PDF parsing.
/// </summary>
internal sealed class ManagedAes128Cbc
{
    /// <summary>AES forward S-box (used during key expansion).</summary>
    private static readonly byte[] SBox = new byte[]
    {
        0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76,
        0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0,
        0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15,
        0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75,
        0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84,
        0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf,
        0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8,
        0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2,
        0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73,
        0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb,
        0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79,
        0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08,
        0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a,
        0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e,
        0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf,
        0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16
    };

    /// <summary>AES inverse S-box (used during block decryption).</summary>
    private static readonly byte[] InvSBox = new byte[]
    {
        0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb,
        0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb,
        0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e,
        0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25,
        0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92,
        0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84,
        0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06,
        0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b,
        0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73,
        0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e,
        0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b,
        0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4,
        0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f,
        0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef,
        0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61,
        0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d
    };

    /// <summary>AES-128 round constants for key schedule (GF(2^8) powers of 2).</summary>
    private static readonly byte[] Rcon = new byte[]
    {
        0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36
    };

    /// <summary>Pre-computed GF(2^8) multiplication table for constant 0x09.</summary>
    private static readonly byte[] Mul9 = BuildGfMulTable(0x09);

    /// <summary>Pre-computed GF(2^8) multiplication table for constant 0x0b.</summary>
    private static readonly byte[] Mul11 = BuildGfMulTable(0x0b);

    /// <summary>Pre-computed GF(2^8) multiplication table for constant 0x0d.</summary>
    private static readonly byte[] Mul13 = BuildGfMulTable(0x0d);

    /// <summary>Pre-computed GF(2^8) multiplication table for constant 0x0e.</summary>
    private static readonly byte[] Mul14 = BuildGfMulTable(0x0e);

    /// <summary>Combined inverse T-table for row 0: merges InvSubBytes + InvMixColumns into a single uint lookup.</summary>
    private static readonly uint[] Td0 = BuildTdTable(Mul14, Mul9, Mul13, Mul11);

    /// <summary>Combined inverse T-table for row 1: merges InvSubBytes + InvMixColumns into a single uint lookup.</summary>
    private static readonly uint[] Td1 = BuildTdTable(Mul11, Mul14, Mul9, Mul13);

    /// <summary>Combined inverse T-table for row 2: merges InvSubBytes + InvMixColumns into a single uint lookup.</summary>
    private static readonly uint[] Td2 = BuildTdTable(Mul13, Mul11, Mul14, Mul9);

    /// <summary>Combined inverse T-table for row 3: merges InvSubBytes + InvMixColumns into a single uint lookup.</summary>
    private static readonly uint[] Td3 = BuildTdTable(Mul9, Mul13, Mul11, Mul14);

    /// <summary>Pre-allocated round-key buffer, filled on each <see cref="Decrypt"/> call.</summary>
    private readonly uint[] _roundKeys = new uint[44];

    /// <summary>Reusable 16-byte block buffer for in-place decryption.</summary>
    private readonly byte[] _state = new byte[16];

    /// <summary>Creates a new <see cref="ManagedAes128Cbc"/> instance with pre-allocated internal buffers.</summary>
    public ManagedAes128Cbc()
    {
    }

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> using AES-128-CBC with the given 16-byte
    /// <paramref name="key"/> and <paramref name="iv"/>, then strips PKCS#7 padding.
    /// </summary>
    /// <param name="key">128-bit (16-byte) AES key.</param>
    /// <param name="iv">128-bit (16-byte) initialisation vector.</param>
    /// <param name="ciphertext">Ciphertext whose length must be a multiple of 16.</param>
    /// <returns>Decrypted plaintext with PKCS#7 padding removed when valid.</returns>
    public byte[] Decrypt(byte[] key, byte[] iv, byte[] ciphertext)
    {
        if (key == null || key.Length != 16)
        {
            throw new ArgumentException("Key must be exactly 16 bytes for AES-128.", nameof(key));
        }
        if (iv == null || iv.Length != 16)
        {
            throw new ArgumentException("IV must be exactly 16 bytes.", nameof(iv));
        }
        if (ciphertext == null || ciphertext.Length == 0 || ciphertext.Length % 16 != 0)
        {
            return ciphertext ?? Array.Empty<byte>();
        }

        ExpandKey(key);
        byte[] plaintext = new byte[ciphertext.Length];
        byte[] state = _state;

        for (int blockStart = 0; blockStart < ciphertext.Length; blockStart += 16)
        {
            Buffer.BlockCopy(ciphertext, blockStart, state, 0, 16);

            DecryptBlock(state);

            // CBC XOR: first block uses IV, subsequent blocks use the previous ciphertext block.
            if (blockStart == 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    plaintext[i] = (byte)(state[i] ^ iv[i]);
                }
            }
            else
            {
                int previousStart = blockStart - 16;
                for (int i = 0; i < 16; i++)
                {
                    plaintext[blockStart + i] = (byte)(state[i] ^ ciphertext[previousStart + i]);
                }
            }
        }

        return RemovePkcs7Padding(plaintext);
    }

    /// <summary>
    /// Removes valid PKCS#7 padding from <paramref name="data"/>.
    /// Returns <paramref name="data"/> unchanged if the padding is not valid.
    /// </summary>
    private static byte[] RemovePkcs7Padding(byte[] data)
    {
        if (data.Length == 0)
        {
            return data;
        }

        int padLength = data[data.Length - 1];
        if (padLength < 1 || padLength > 16)
        {
            return data;
        }

        for (int i = data.Length - padLength; i < data.Length; i++)
        {
            if (data[i] != padLength)
            {
                return data;
            }
        }

        byte[] unpadded = new byte[data.Length - padLength];
        Buffer.BlockCopy(data, 0, unpadded, 0, unpadded.Length);
        return unpadded;
    }

    /// <summary>
    /// AES-128 key schedule: produces 44 round-key words in <see cref="_roundKeys"/>.
    /// Rounds 1–9 are pre-transformed with InvMixColumns for the equivalent inverse cipher
    /// (FIPS 197, §5.3.5), enabling T-table-based decryption.
    /// </summary>
    private void ExpandKey(byte[] key)
    {
        // AES-128: Nk=4, Nr=10, total words = 4 * (Nr+1) = 44
        uint[] w = _roundKeys;

        for (int i = 0; i < 4; i++)
        {
            w[i] = (uint)(
                (key[i * 4 + 0] << 24) |
                (key[i * 4 + 1] << 16) |
                (key[i * 4 + 2] << 8) |
                key[i * 4 + 3]);
        }

        for (int i = 4; i < 44; i++)
        {
            uint temp = w[i - 1];
            if (i % 4 == 0)
            {
                // RotWord + SubWord + XOR with Rcon
                temp = SubWord(RotWord(temp)) ^ ((uint)Rcon[i / 4 - 1] << 24);
            }
            w[i] = w[i - 4] ^ temp;
        }

        // Apply InvMixColumns to round keys 1–9 for equivalent inverse cipher.
        for (int round = 1; round <= 9; round++)
        {
            for (int col = 0; col < 4; col++)
            {
                int idx = round * 4 + col;
                uint word = w[idx];
                byte b0 = (byte)(word >> 24);
                byte b1 = (byte)(word >> 16);
                byte b2 = (byte)(word >> 8);
                byte b3 = (byte)word;
                w[idx] =
                    ((uint)(Mul14[b0] ^ Mul11[b1] ^ Mul13[b2] ^ Mul9[b3]) << 24) |
                    ((uint)(Mul9[b0] ^ Mul14[b1] ^ Mul11[b2] ^ Mul13[b3]) << 16) |
                    ((uint)(Mul13[b0] ^ Mul9[b1] ^ Mul14[b2] ^ Mul11[b3]) << 8) |
                    (uint)(Mul11[b0] ^ Mul13[b1] ^ Mul9[b2] ^ Mul14[b3]);
            }
        }
    }

    /// <summary>
    /// Decrypts a single 16-byte AES block in place using T-tables (equivalent inverse cipher).
    /// The state buffer uses column-major layout (index = col*4 + row). InvShiftRows,
    /// InvSubBytes, and InvMixColumns are fused into T-table lookups for rounds 1–9.
    /// </summary>
    private void DecryptBlock(byte[] state)
    {
        uint[] rk = _roundKeys;

        // Pack state bytes into column words (big-endian: row 0 in MSB) and apply initial round key (round 10).
        uint c0 = ((uint)state[0] << 24 | (uint)state[1] << 16 | (uint)state[2] << 8 | state[3]) ^ rk[40];
        uint c1 = ((uint)state[4] << 24 | (uint)state[5] << 16 | (uint)state[6] << 8 | state[7]) ^ rk[41];
        uint c2 = ((uint)state[8] << 24 | (uint)state[9] << 16 | (uint)state[10] << 8 | state[11]) ^ rk[42];
        uint c3 = ((uint)state[12] << 24 | (uint)state[13] << 16 | (uint)state[14] << 8 | state[15]) ^ rk[43];

        // Rounds 9 down to 1: T-table lookups with InvShiftRows built into the indexing pattern.
        // Each output column j reads: row 0 from c_j, row 1 from c_{(j+3)%4}, row 2 from c_{(j+2)%4}, row 3 from c_{(j+1)%4}.
        for (int round = 9; round >= 1; round--)
        {
            int ki = round * 4;
            uint t0 = Td0[(c0 >> 24) & 0xFF] ^ Td1[(c3 >> 16) & 0xFF] ^ Td2[(c2 >> 8) & 0xFF] ^ Td3[c1 & 0xFF] ^ rk[ki];
            uint t1 = Td0[(c1 >> 24) & 0xFF] ^ Td1[(c0 >> 16) & 0xFF] ^ Td2[(c3 >> 8) & 0xFF] ^ Td3[c2 & 0xFF] ^ rk[ki + 1];
            uint t2 = Td0[(c2 >> 24) & 0xFF] ^ Td1[(c1 >> 16) & 0xFF] ^ Td2[(c0 >> 8) & 0xFF] ^ Td3[c3 & 0xFF] ^ rk[ki + 2];
            uint t3 = Td0[(c3 >> 24) & 0xFF] ^ Td1[(c2 >> 16) & 0xFF] ^ Td2[(c1 >> 8) & 0xFF] ^ Td3[c0 & 0xFF] ^ rk[ki + 3];
            c0 = t0;
            c1 = t1;
            c2 = t2;
            c3 = t3;
        }

        // Final round (round 0): InvSubBytes + InvShiftRows + AddRoundKey, no InvMixColumns.
        uint k0 = rk[0];
        uint k1 = rk[1];
        uint k2 = rk[2];
        uint k3 = rk[3];

        state[0] = (byte)(InvSBox[(c0 >> 24) & 0xFF] ^ (k0 >> 24));
        state[1] = (byte)(InvSBox[(c3 >> 16) & 0xFF] ^ (k0 >> 16));
        state[2] = (byte)(InvSBox[(c2 >> 8) & 0xFF] ^ (k0 >> 8));
        state[3] = (byte)(InvSBox[c1 & 0xFF] ^ k0);

        state[4] = (byte)(InvSBox[(c1 >> 24) & 0xFF] ^ (k1 >> 24));
        state[5] = (byte)(InvSBox[(c0 >> 16) & 0xFF] ^ (k1 >> 16));
        state[6] = (byte)(InvSBox[(c3 >> 8) & 0xFF] ^ (k1 >> 8));
        state[7] = (byte)(InvSBox[c2 & 0xFF] ^ k1);

        state[8] = (byte)(InvSBox[(c2 >> 24) & 0xFF] ^ (k2 >> 24));
        state[9] = (byte)(InvSBox[(c1 >> 16) & 0xFF] ^ (k2 >> 16));
        state[10] = (byte)(InvSBox[(c0 >> 8) & 0xFF] ^ (k2 >> 8));
        state[11] = (byte)(InvSBox[c3 & 0xFF] ^ k2);

        state[12] = (byte)(InvSBox[(c3 >> 24) & 0xFF] ^ (k3 >> 24));
        state[13] = (byte)(InvSBox[(c2 >> 16) & 0xFF] ^ (k3 >> 16));
        state[14] = (byte)(InvSBox[(c1 >> 8) & 0xFF] ^ (k3 >> 8));
        state[15] = (byte)(InvSBox[c0 & 0xFF] ^ k3);
    }

    /// <summary>
    /// Builds a 256-entry lookup table for GF(2^8) multiplication by <paramref name="constant"/>.
    /// Called once during static initialisation to populate <see cref="Mul9"/>, <see cref="Mul11"/>,
    /// <see cref="Mul13"/>, and <see cref="Mul14"/>.
    /// </summary>
    private static byte[] BuildGfMulTable(int constant)
    {
        byte[] table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            table[i] = GfMul(constant, (byte)i);
        }

        return table;
    }

    /// <summary>
    /// Builds a 256-entry combined T-table that fuses InvSubBytes and one row of InvMixColumns.
    /// Each entry packs four result bytes into a single <see cref="uint"/> for the given row’s
    /// multiplication constants.
    /// </summary>
    private static uint[] BuildTdTable(byte[] mulRow0, byte[] mulRow1, byte[] mulRow2, byte[] mulRow3)
    {
        uint[] table = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            byte s = InvSBox[i];
            table[i] = ((uint)mulRow0[s] << 24) | ((uint)mulRow1[s] << 16) | ((uint)mulRow2[s] << 8) | mulRow3[s];
        }

        return table;
    }

    /// <summary>
    /// Galois Field multiplication in GF(2^8) with irreducible polynomial
    /// x^8 + x^4 + x^3 + x + 1 (0x11b), using the Russian peasant algorithm.
    /// Only used during static initialisation for building lookup tables.
    /// </summary>
    private static byte GfMul(int a, byte b)
    {
        int result = 0;
        int bValue = b;

        while (a != 0)
        {
            if ((a & 1) != 0)
            {
                result ^= bValue;
            }

            bool highBitSet = (bValue & 0x80) != 0;
            bValue = (bValue << 1) & 0xFF;
            if (highBitSet)
            {
                bValue ^= 0x1b;
            }

            a >>= 1;
        }

        return (byte)result;
    }

    /// <summary>Rotates a 32-bit word left by 8 bits (used in key schedule).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotWord(uint w)
    {
        return (w << 8) | (w >> 24);
    }

    /// <summary>Substitutes each byte of a 32-bit word through the AES S-box (used in key schedule).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SubWord(uint w)
    {
        return ((uint)SBox[(w >> 24) & 0xFF] << 24) |
               ((uint)SBox[(w >> 16) & 0xFF] << 16) |
               ((uint)SBox[(w >> 8) & 0xFF] << 8) |
               (uint)SBox[w & 0xFF];
    }
}
