using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption;

/// <summary>
/// Pure managed AES-128-CBC encryption with no padding.
/// Used only by the R6 hardened-hash algorithm (ISO 32000-2 Algorithm 2.B).
/// </summary>
internal sealed class ManagedAes128CbcEncryptor
{
    private readonly uint[] _roundKeys = new uint[44];
    private readonly byte[] _state = new byte[16];

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using AES-128-CBC with the given 16-byte
    /// <paramref name="key"/> and <paramref name="iv"/>. No padding is applied; the input
    /// length must already be a multiple of 16.
    /// </summary>
    public byte[] Encrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        if (key == null || key.Length != 16)
        {
            throw new ArgumentException("Key must be exactly 16 bytes for AES-128.", nameof(key));
        }

        if (iv == null || iv.Length != 16)
        {
            throw new ArgumentException("IV must be exactly 16 bytes.", nameof(iv));
        }

        if (plaintext == null || plaintext.Length % 16 != 0)
        {
            throw new ArgumentException("Plaintext length must be a non-zero multiple of 16 bytes.", nameof(plaintext));
        }

        ExpandKey(key);
        var ciphertext = new byte[plaintext.Length];
        byte[] state = _state;

        for (int blockStart = 0; blockStart < plaintext.Length; blockStart += 16)
        {
            for (int i = 0; i < 16; i++)
            {
                byte previousByte = (blockStart == 0) ? iv[i] : ciphertext[blockStart - 16 + i];
                state[i] = (byte)(plaintext[blockStart + i] ^ previousByte);
            }

            EncryptBlock(state);
            Buffer.BlockCopy(state, 0, ciphertext, blockStart, 16);
        }

        return ciphertext;
    }

    /// <summary>
    /// AES-128 forward key schedule: produces 44 round-key words in <see cref="_roundKeys"/>.
    /// </summary>
    private void ExpandKey(byte[] key)
    {
        uint[] w = _roundKeys;

        for (int i = 0; i < 4; i++)
        {
            w[i] = (uint)(
                (key[(i * 4) + 0] << 24)
                    | (key[(i * 4) + 1] << 16)
                    | (key[(i * 4) + 2] << 8)
                    | key[(i * 4) + 3]);
        }

        for (int i = 4; i < 44; i++)
        {
            uint temp = w[i - 1];
            if (i % 4 == 0)
            {
                temp = SubWord(RotWord(temp)) ^ ((uint)AesTables.Rcon[(i / 4) - 1] << 24);
            }

            w[i] = w[i - 4] ^ temp;
        }
    }

    /// <summary>
    /// Encrypts a single 16-byte AES block in place (standard forward cipher, FIPS 197 §5.1).
    /// The state buffer uses column-major layout (index = col*4 + row).
    /// </summary>
    private void EncryptBlock(byte[] state)
    {
        uint[] rk = _roundKeys;

        AddRoundKey(state, rk, 0);

        for (int round = 1; round <= 9; round++)
        {
            SubBytes(state);
            ShiftRows(state);
            MixColumns(state);
            AddRoundKey(state, rk, round * 4);
        }

        SubBytes(state);
        ShiftRows(state);
        AddRoundKey(state, rk, 40);
    }

    private static void AddRoundKey(byte[] state, uint[] roundKeys, int wordOffset)
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

    private static void SubBytes(byte[] state)
    {
        for (int i = 0; i < 16; i++)
        {
            state[i] = AesTables.SBox[state[i]];
        }
    }

    private static void ShiftRows(byte[] state)
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

    private static void MixColumns(byte[] state)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotWord(uint w) => (w << 8) | (w >> 24);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SubWord(uint w)
    {
        return ((uint)AesTables.SBox[(w >> 24) & 0xFF] << 24)
            | ((uint)AesTables.SBox[(w >> 16) & 0xFF] << 16)
            | ((uint)AesTables.SBox[(w >> 8) & 0xFF] << 8)
            | (uint)AesTables.SBox[w & 0xFF];
    }
}
