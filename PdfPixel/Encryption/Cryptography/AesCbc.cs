using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption.Cryptography;

/// <summary>
/// Pure managed AES-128/AES-256 in CBC mode.
/// Used in place of <see cref="System.Security.Cryptography.Aes"/> to support
/// platforms where the native implementation is unavailable (e.g., Blazor WASM).
/// </summary>
internal static class AesCbc
{
    private const int BlockSize = 16;

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> using AES-CBC with the given 16- or 32-byte
    /// <paramref name="key"/> and 16-byte <paramref name="iv"/>.
    /// </summary>
    /// <param name="key">128-bit (16-byte) or 256-bit (32-byte) AES key.</param>
    /// <param name="iv">128-bit (16-byte) initialisation vector.</param>
    /// <param name="ciphertext">Ciphertext whose length must be a multiple of 16.</param>
    /// <param name="stripPkcs7Padding">Whether to remove PKCS#7 padding from the result.</param>
    /// <returns>Decrypted plaintext, with PKCS#7 padding removed when <paramref name="stripPkcs7Padding"/> is <see langword="true"/> and the padding is valid.</returns>
    public static byte[] Decrypt(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> iv, in ReadOnlySpan<byte> ciphertext, bool stripPkcs7Padding)
    {
        int rounds = GetRounds(key, iv);
        if (ciphertext.Length == 0 || ciphertext.Length % BlockSize != 0)
        {
            return ciphertext.ToArray();
        }

        Span<uint> roundKeys = stackalloc uint[4 * (rounds + 1)];
        ExpandKey(key, roundKeys);
        InvertMixColumns(roundKeys, rounds);

        var plaintext = new byte[ciphertext.Length];
        Span<byte> state = stackalloc byte[BlockSize];
        for (int blockStart = 0; blockStart < ciphertext.Length; blockStart += BlockSize)
        {
            ciphertext.Slice(blockStart, BlockSize).CopyTo(state);
            DecryptBlock(state, roundKeys, rounds);

            ReadOnlySpan<byte> previousBlock = (blockStart == 0) ? iv : ciphertext.Slice(blockStart - BlockSize, BlockSize);
            for (int i = 0; i < BlockSize; i++)
            {
                plaintext[blockStart + i] = (byte)(state[i] ^ previousBlock[i]);
            }
        }

        return stripPkcs7Padding ? RemovePkcs7Padding(plaintext) : plaintext;
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using AES-CBC with the given 16- or 32-byte
    /// <paramref name="key"/> and 16-byte <paramref name="iv"/>. No padding is applied; the input
    /// length must already be a multiple of 16.
    /// </summary>
    public static byte[] Encrypt(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> iv, in ReadOnlySpan<byte> plaintext)
    {
        int rounds = GetRounds(key, iv);
        if (plaintext.Length % BlockSize != 0)
        {
            throw new ArgumentException("Plaintext length must be a multiple of 16 bytes.", nameof(plaintext));
        }

        Span<uint> roundKeys = stackalloc uint[4 * (rounds + 1)];
        ExpandKey(key, roundKeys);

        var ciphertext = new byte[plaintext.Length];
        Span<byte> state = stackalloc byte[BlockSize];
        for (int blockStart = 0; blockStart < plaintext.Length; blockStart += BlockSize)
        {
            ReadOnlySpan<byte> previousBlock = (blockStart == 0) ? iv : ciphertext.AsSpan(blockStart - BlockSize, BlockSize);
            for (int i = 0; i < BlockSize; i++)
            {
                state[i] = (byte)(plaintext[blockStart + i] ^ previousBlock[i]);
            }

            EncryptBlock(state, roundKeys, rounds);
            state.CopyTo(ciphertext.AsSpan(blockStart, BlockSize));
        }

        return ciphertext;
    }

    private static int GetRounds(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> iv)
    {
        if (key.Length != 16 && key.Length != 32)
        {
            throw new ArgumentException("Key must be exactly 16 or 32 bytes.", nameof(key));
        }

        if (iv.Length != BlockSize)
        {
            throw new ArgumentException("IV must be exactly 16 bytes.", nameof(iv));
        }

        return (key.Length / 4) + 6;
    }

    /// <summary>
    /// Removes valid PKCS#7 padding from <paramref name="data"/>.
    /// Returns <paramref name="data"/> unchanged if the padding is not valid.
    /// </summary>
    private static byte[] RemovePkcs7Padding(byte[] data)
    {
        int padLength = data[data.Length - 1];
        if (padLength < 1 || padLength > BlockSize)
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

        return data.AsSpan(0, data.Length - padLength).ToArray();
    }

    /// <summary>
    /// AES forward key schedule (FIPS 197, §5.2).
    /// </summary>
    private static void ExpandKey(in ReadOnlySpan<byte> key, in Span<uint> roundKeys)
    {
        int keyWords = key.Length / 4;
        for (int i = 0; i < keyWords; i++)
        {
            roundKeys[i] = (uint)(
                (key[(i * 4) + 0] << 24)
                    | (key[(i * 4) + 1] << 16)
                    | (key[(i * 4) + 2] << 8)
                    | key[(i * 4) + 3]);
        }

        for (int i = keyWords; i < roundKeys.Length; i++)
        {
            uint temp = roundKeys[i - 1];
            if (i % keyWords == 0)
            {
                temp = SubWord(RotWord(temp)) ^ ((uint)AesTables.Rcon[(i / keyWords) - 1] << 24);
            }
            else if (keyWords > 6 && i % keyWords == 4)
            {
                temp = SubWord(temp);
            }

            roundKeys[i] = roundKeys[i - keyWords] ^ temp;
        }
    }

    /// <summary>
    /// Applies InvMixColumns to round keys 1 to Nr-1 for the equivalent inverse cipher (FIPS 197, §5.3.5).
    /// </summary>
    private static void InvertMixColumns(in Span<uint> roundKeys, int rounds)
    {
        for (int index = 4; index < rounds * 4; index++)
        {
            uint word = roundKeys[index];
            var b0 = (byte)(word >> 24);
            var b1 = (byte)(word >> 16);
            var b2 = (byte)(word >> 8);
            var b3 = (byte)word;
            roundKeys[index] =
                ((uint)(AesTables.Mul14[b0] ^ AesTables.Mul11[b1] ^ AesTables.Mul13[b2] ^ AesTables.Mul9[b3]) << 24)
                    | ((uint)(AesTables.Mul9[b0] ^ AesTables.Mul14[b1] ^ AesTables.Mul11[b2] ^ AesTables.Mul13[b3]) << 16)
                    | ((uint)(AesTables.Mul13[b0] ^ AesTables.Mul9[b1] ^ AesTables.Mul14[b2] ^ AesTables.Mul11[b3]) << 8)
                    | (uint)(AesTables.Mul11[b0] ^ AesTables.Mul13[b1] ^ AesTables.Mul9[b2] ^ AesTables.Mul14[b3]);
        }
    }

    /// <summary>
    /// Decrypts a single 16-byte AES block in place using T-tables (equivalent inverse cipher).
    /// The state buffer uses column-major layout (index = col*4 + row). InvShiftRows,
    /// InvSubBytes, and InvMixColumns are fused into T-table lookups for rounds Nr-1 to 1.
    /// </summary>
    private static void DecryptBlock(in Span<byte> state, in ReadOnlySpan<uint> roundKeys, int rounds)
    {
        int lastKey = rounds * 4;

        // Pack state bytes into column words (big-endian: row 0 in MSB) and apply the last round key.
        uint c0 = ((uint)state[0] << 24 | (uint)state[1] << 16 | (uint)state[2] << 8 | state[3]) ^ roundKeys[lastKey];
        uint c1 = ((uint)state[4] << 24 | (uint)state[5] << 16 | (uint)state[6] << 8 | state[7]) ^ roundKeys[lastKey + 1];
        uint c2 = ((uint)state[8] << 24 | (uint)state[9] << 16 | (uint)state[10] << 8 | state[11]) ^ roundKeys[lastKey + 2];
        uint c3 = ((uint)state[12] << 24 | (uint)state[13] << 16 | (uint)state[14] << 8 | state[15]) ^ roundKeys[lastKey + 3];

        // Each output column j reads: row 0 from c_j, row 1 from c_{(j+3)%4}, row 2 from c_{(j+2)%4}, row 3 from c_{(j+1)%4}.
        for (int round = rounds - 1; round >= 1; round--)
        {
            int ki = round * 4;
            uint t0 = AesTables.Td0[(c0 >> 24) & 0xFF] ^ AesTables.Td1[(c3 >> 16) & 0xFF] ^ AesTables.Td2[(c2 >> 8) & 0xFF] ^ AesTables.Td3[c1 & 0xFF] ^ roundKeys[ki];
            uint t1 = AesTables.Td0[(c1 >> 24) & 0xFF] ^ AesTables.Td1[(c0 >> 16) & 0xFF] ^ AesTables.Td2[(c3 >> 8) & 0xFF] ^ AesTables.Td3[c2 & 0xFF] ^ roundKeys[ki + 1];
            uint t2 = AesTables.Td0[(c2 >> 24) & 0xFF] ^ AesTables.Td1[(c1 >> 16) & 0xFF] ^ AesTables.Td2[(c0 >> 8) & 0xFF] ^ AesTables.Td3[c3 & 0xFF] ^ roundKeys[ki + 2];
            uint t3 = AesTables.Td0[(c3 >> 24) & 0xFF] ^ AesTables.Td1[(c2 >> 16) & 0xFF] ^ AesTables.Td2[(c1 >> 8) & 0xFF] ^ AesTables.Td3[c0 & 0xFF] ^ roundKeys[ki + 3];
            c0 = t0;
            c1 = t1;
            c2 = t2;
            c3 = t3;
        }

        // Final round (round 0): InvSubBytes + InvShiftRows + AddRoundKey, no InvMixColumns.
        uint k0 = roundKeys[0];
        uint k1 = roundKeys[1];
        uint k2 = roundKeys[2];
        uint k3 = roundKeys[3];

        state[0] = (byte)(AesTables.InvSBox[(c0 >> 24) & 0xFF] ^ (k0 >> 24));
        state[1] = (byte)(AesTables.InvSBox[(c3 >> 16) & 0xFF] ^ (k0 >> 16));
        state[2] = (byte)(AesTables.InvSBox[(c2 >> 8) & 0xFF] ^ (k0 >> 8));
        state[3] = (byte)(AesTables.InvSBox[c1 & 0xFF] ^ k0);

        state[4] = (byte)(AesTables.InvSBox[(c1 >> 24) & 0xFF] ^ (k1 >> 24));
        state[5] = (byte)(AesTables.InvSBox[(c0 >> 16) & 0xFF] ^ (k1 >> 16));
        state[6] = (byte)(AesTables.InvSBox[(c3 >> 8) & 0xFF] ^ (k1 >> 8));
        state[7] = (byte)(AesTables.InvSBox[c2 & 0xFF] ^ k1);

        state[8] = (byte)(AesTables.InvSBox[(c2 >> 24) & 0xFF] ^ (k2 >> 24));
        state[9] = (byte)(AesTables.InvSBox[(c1 >> 16) & 0xFF] ^ (k2 >> 16));
        state[10] = (byte)(AesTables.InvSBox[(c0 >> 8) & 0xFF] ^ (k2 >> 8));
        state[11] = (byte)(AesTables.InvSBox[c3 & 0xFF] ^ k2);

        state[12] = (byte)(AesTables.InvSBox[(c3 >> 24) & 0xFF] ^ (k3 >> 24));
        state[13] = (byte)(AesTables.InvSBox[(c2 >> 16) & 0xFF] ^ (k3 >> 16));
        state[14] = (byte)(AesTables.InvSBox[(c1 >> 8) & 0xFF] ^ (k3 >> 8));
        state[15] = (byte)(AesTables.InvSBox[c0 & 0xFF] ^ k3);
    }

    /// <summary>
    /// Encrypts a single 16-byte AES block in place (standard forward cipher, FIPS 197 §5.1).
    /// The state buffer uses column-major layout (index = col*4 + row).
    /// </summary>
    private static void EncryptBlock(in Span<byte> state, in ReadOnlySpan<uint> roundKeys, int rounds)
    {
        AddRoundKey(state, roundKeys, 0);

        for (int round = 1; round < rounds; round++)
        {
            SubBytes(state);
            ShiftRows(state);
            MixColumns(state);
            AddRoundKey(state, roundKeys, round * 4);
        }

        SubBytes(state);
        ShiftRows(state);
        AddRoundKey(state, roundKeys, rounds * 4);
    }

    private static void AddRoundKey(in Span<byte> state, in ReadOnlySpan<uint> roundKeys, int wordOffset)
    {
        for (int col = 0; col < 4; col++)
        {
            uint word = roundKeys[wordOffset + col];
            state[(col * 4) + 0] ^= (byte)(word >> 24);
            state[(col * 4) + 1] ^= (byte)(word >> 16);
            state[(col * 4) + 2] ^= (byte)(word >> 8);
            state[(col * 4) + 3] ^= (byte)word;
        }
    }

    private static void SubBytes(in Span<byte> state)
    {
        for (int i = 0; i < BlockSize; i++)
        {
            state[i] = AesTables.SBox[state[i]];
        }
    }

    private static void ShiftRows(in Span<byte> state)
    {
        // Row r (0-based) is cyclically shifted left by r columns. State is column-major: index = col*4 + row.
        byte row1Col0 = state[1];
        state[1] = state[5];
        state[5] = state[9];
        state[9] = state[13];
        state[13] = row1Col0;

        byte row2Col0 = state[2];
        byte row2Col1 = state[6];
        state[2] = state[10];
        state[6] = state[14];
        state[10] = row2Col0;
        state[14] = row2Col1;

        byte row3Col0 = state[3];
        state[3] = state[15];
        state[15] = state[11];
        state[11] = state[7];
        state[7] = row3Col0;
    }

    private static void MixColumns(in Span<byte> state)
    {
        for (int col = 0; col < 4; col++)
        {
            int baseIndex = col * 4;
            byte s0 = state[baseIndex + 0];
            byte s1 = state[baseIndex + 1];
            byte s2 = state[baseIndex + 2];
            byte s3 = state[baseIndex + 3];

            state[baseIndex + 0] = (byte)(AesTables.Mul2[s0] ^ AesTables.Mul3[s1] ^ s2 ^ s3);
            state[baseIndex + 1] = (byte)(s0 ^ AesTables.Mul2[s1] ^ AesTables.Mul3[s2] ^ s3);
            state[baseIndex + 2] = (byte)(s0 ^ s1 ^ AesTables.Mul2[s2] ^ AesTables.Mul3[s3]);
            state[baseIndex + 3] = (byte)(AesTables.Mul3[s0] ^ s1 ^ s2 ^ AesTables.Mul2[s3]);
        }
    }

    /// <summary>
    /// Rotates a 32-bit word left by 8 bits (used in key schedule).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotWord(uint w) => (w << 8) | (w >> 24);

    /// <summary>
    /// Substitutes each byte of a 32-bit word through the AES S-box (used in key schedule).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SubWord(uint w)
    {
        return ((uint)AesTables.SBox[(w >> 24) & 0xFF] << 24)
            | ((uint)AesTables.SBox[(w >> 16) & 0xFF] << 16)
            | ((uint)AesTables.SBox[(w >> 8) & 0xFF] << 8)
            | (uint)AesTables.SBox[w & 0xFF];
    }
}
