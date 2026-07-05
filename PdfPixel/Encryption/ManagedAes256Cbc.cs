using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Encryption;

/// <summary>
/// Pure managed AES-256-CBC decryption, optionally with PKCS#7 unpadding.
/// Used in place of <see cref="System.Security.Cryptography.Aes"/> to support
/// platforms where the native implementation is unavailable (e.g., Blazor WASM).
/// Only decryption is implemented; encryption is not required for PDF parsing.
/// </summary>
internal sealed class ManagedAes256Cbc
{
    /// <summary>
    /// Pre-allocated round-key buffer, filled on each <see cref="Decrypt"/> call.
    /// </summary>
    private readonly uint[] _roundKeys = new uint[60];

    /// <summary>
    /// Reusable 16-byte block buffer for in-place decryption.
    /// </summary>
    private readonly byte[] _state = new byte[16];

    /// <summary>
    /// Creates a new <see cref="ManagedAes256Cbc"/> instance with pre-allocated internal buffers.
    /// </summary>
    public ManagedAes256Cbc()
    {
    }

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> using AES-256-CBC with the given 32-byte
    /// <paramref name="key"/> and 16-byte <paramref name="iv"/>.
    /// </summary>
    /// <param name="key">256-bit (32-byte) AES key.</param>
    /// <param name="iv">128-bit (16-byte) initialisation vector.</param>
    /// <param name="ciphertext">Ciphertext whose length must be a multiple of 16.</param>
    /// <param name="stripPkcs7Padding">Whether to remove PKCS#7 padding from the result. Set to <see langword="false"/> for the fixed-length key-unwrap operations (/UE, /OE) which use no padding.</param>
    /// <returns>Decrypted plaintext, with PKCS#7 padding removed when <paramref name="stripPkcs7Padding"/> is <see langword="true"/> and the padding is valid.</returns>
    public byte[] Decrypt(byte[] key, byte[] iv, byte[] ciphertext, bool stripPkcs7Padding)
    {
        if (key == null || key.Length != 32)
        {
            throw new ArgumentException("Key must be exactly 32 bytes for AES-256.", nameof(key));
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
        var plaintext = new byte[ciphertext.Length];
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

        return stripPkcs7Padding ? RemovePkcs7Padding(plaintext) : plaintext;
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

        var unpadded = new byte[data.Length - padLength];
        Buffer.BlockCopy(data, 0, unpadded, 0, unpadded.Length);
        return unpadded;
    }

    /// <summary>
    /// AES-256 key schedule: produces 60 round-key words in <see cref="_roundKeys"/>.
    /// Rounds 1–13 are pre-transformed with InvMixColumns for the equivalent inverse cipher
    /// (FIPS 197, §5.3.5), enabling T-table-based decryption.
    /// </summary>
    private void ExpandKey(byte[] key)
    {
        // AES-256: Nk=8, Nr=14, total words = 4 * (Nr+1) = 60
        uint[] w = _roundKeys;

        for (int i = 0; i < 8; i++)
        {
            w[i] = (uint)(
                (key[(i * 4) + 0] << 24)
                    | (key[(i * 4) + 1] << 16)
                    | (key[(i * 4) + 2] << 8)
                    | key[(i * 4) + 3]);
        }

        for (int i = 8; i < 60; i++)
        {
            uint temp = w[i - 1];
            if (i % 8 == 0)
            {
                // RotWord + SubWord + XOR with Rcon
                temp = SubWord(RotWord(temp)) ^ ((uint)AesTables.Rcon[(i / 8) - 1] << 24);
            }
            else if (i % 8 == 4)
            {
                temp = SubWord(temp);
            }

            w[i] = w[i - 8] ^ temp;
        }

        // Apply InvMixColumns to round keys 1–13 for equivalent inverse cipher.
        for (int round = 1; round <= 13; round++)
        {
            for (int col = 0; col < 4; col++)
            {
                int idx = (round * 4) + col;
                uint word = w[idx];
                var b0 = (byte)(word >> 24);
                var b1 = (byte)(word >> 16);
                var b2 = (byte)(word >> 8);
                var b3 = (byte)word;
                w[idx] =
                    ((uint)(AesTables.Mul14[b0] ^ AesTables.Mul11[b1] ^ AesTables.Mul13[b2] ^ AesTables.Mul9[b3]) << 24)
                        | ((uint)(AesTables.Mul9[b0] ^ AesTables.Mul14[b1] ^ AesTables.Mul11[b2] ^ AesTables.Mul13[b3]) << 16)
                        | ((uint)(AesTables.Mul13[b0] ^ AesTables.Mul9[b1] ^ AesTables.Mul14[b2] ^ AesTables.Mul11[b3]) << 8)
                        | (uint)(AesTables.Mul11[b0] ^ AesTables.Mul13[b1] ^ AesTables.Mul9[b2] ^ AesTables.Mul14[b3]);
            }
        }
    }

    /// <summary>
    /// Decrypts a single 16-byte AES block in place using T-tables (equivalent inverse cipher).
    /// The state buffer uses column-major layout (index = col*4 + row). InvShiftRows,
    /// InvSubBytes, and InvMixColumns are fused into T-table lookups for rounds 1–13.
    /// </summary>
    private void DecryptBlock(byte[] state)
    {
        uint[] rk = _roundKeys;

        // Pack state bytes into column words (big-endian: row 0 in MSB) and apply initial round key (round 14).
        uint c0 = ((uint)state[0] << 24 | (uint)state[1] << 16 | (uint)state[2] << 8 | state[3]) ^ rk[56];
        uint c1 = ((uint)state[4] << 24 | (uint)state[5] << 16 | (uint)state[6] << 8 | state[7]) ^ rk[57];
        uint c2 = ((uint)state[8] << 24 | (uint)state[9] << 16 | (uint)state[10] << 8 | state[11]) ^ rk[58];
        uint c3 = ((uint)state[12] << 24 | (uint)state[13] << 16 | (uint)state[14] << 8 | state[15]) ^ rk[59];

        // Rounds 13 down to 1: T-table lookups with InvShiftRows built into the indexing pattern.
        // Each output column j reads: row 0 from c_j, row 1 from c_{(j+3)%4}, row 2 from c_{(j+2)%4}, row 3 from c_{(j+1)%4}.
        for (int round = 13; round >= 1; round--)
        {
            int ki = round * 4;
            uint t0 = AesTables.Td0[(c0 >> 24) & 0xFF] ^ AesTables.Td1[(c3 >> 16) & 0xFF] ^ AesTables.Td2[(c2 >> 8) & 0xFF] ^ AesTables.Td3[c1 & 0xFF] ^ rk[ki];
            uint t1 = AesTables.Td0[(c1 >> 24) & 0xFF] ^ AesTables.Td1[(c0 >> 16) & 0xFF] ^ AesTables.Td2[(c3 >> 8) & 0xFF] ^ AesTables.Td3[c2 & 0xFF] ^ rk[ki + 1];
            uint t2 = AesTables.Td0[(c2 >> 24) & 0xFF] ^ AesTables.Td1[(c1 >> 16) & 0xFF] ^ AesTables.Td2[(c0 >> 8) & 0xFF] ^ AesTables.Td3[c3 & 0xFF] ^ rk[ki + 2];
            uint t3 = AesTables.Td0[(c3 >> 24) & 0xFF] ^ AesTables.Td1[(c2 >> 16) & 0xFF] ^ AesTables.Td2[(c1 >> 8) & 0xFF] ^ AesTables.Td3[c0 & 0xFF] ^ rk[ki + 3];
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
