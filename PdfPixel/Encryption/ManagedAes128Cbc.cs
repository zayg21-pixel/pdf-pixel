using System;

namespace PdfPixel.Encryption
{
    // TODO: improve performance here and in MD5.
    /// <summary>
    /// Pure managed AES-128-CBC decryption with PKCS#7 unpadding.
    /// Used in place of <see cref="System.Security.Cryptography.Aes"/> to support
    /// platforms where the native implementation is unavailable (e.g., Blazor WASM).
    /// Only decryption is implemented; encryption is not required for PDF parsing.
    /// </summary>
    internal static class ManagedAes128Cbc
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

        /// <summary>
        /// Decrypts <paramref name="ciphertext"/> using AES-128-CBC with the given 16-byte
        /// <paramref name="key"/> and <paramref name="iv"/>, then strips PKCS#7 padding.
        /// </summary>
        /// <param name="key">128-bit (16-byte) AES key.</param>
        /// <param name="iv">128-bit (16-byte) initialisation vector.</param>
        /// <param name="ciphertext">Ciphertext whose length must be a multiple of 16.</param>
        /// <returns>Decrypted plaintext with PKCS#7 padding removed when valid.</returns>
        public static byte[] Decrypt(byte[] key, byte[] iv, byte[] ciphertext)
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

            uint[] roundKeys = ExpandKey(key);
            byte[] plaintext = new byte[ciphertext.Length];
            byte[] previousBlock = iv;

            for (int blockStart = 0; blockStart < ciphertext.Length; blockStart += 16)
            {
                byte[] cipherBlock = new byte[16];
                Buffer.BlockCopy(ciphertext, blockStart, cipherBlock, 0, 16);

                byte[] plainBlock = DecryptBlock(cipherBlock, roundKeys);

                for (int i = 0; i < 16; i++)
                {
                    plaintext[blockStart + i] = (byte)(plainBlock[i] ^ previousBlock[i]);
                }

                previousBlock = cipherBlock;
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
        /// AES-128 key schedule: produces 44 round-key words packed as 11 sets of 4 words.
        /// The returned array is indexed as <c>roundKeys[round * 4 + col]</c>.
        /// </summary>
        private static uint[] ExpandKey(byte[] key)
        {
            // AES-128: Nk=4, Nr=10, total words = 4 * (Nr+1) = 44
            uint[] w = new uint[44];

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

            return w;
        }

        /// <summary>Decrypts a single 16-byte AES block using the expanded round keys.</summary>
        private static byte[] DecryptBlock(byte[] block, uint[] roundKeys)
        {
            // Load block into state matrix (column-major: state[row, col] = block[col*4 + row]).
            byte[,] state = new byte[4, 4];
            for (int col = 0; col < 4; col++)
            {
                for (int row = 0; row < 4; row++)
                {
                    state[row, col] = block[col * 4 + row];
                }
            }

            // Initial round key addition (round 10).
            AddRoundKey(state, roundKeys, 10);

            // Main decryption rounds 9 down to 1.
            for (int round = 9; round >= 1; round--)
            {
                InvShiftRows(state);
                InvSubBytes(state);
                AddRoundKey(state, roundKeys, round);
                InvMixColumns(state);
            }

            // Final round: no InvMixColumns.
            InvShiftRows(state);
            InvSubBytes(state);
            AddRoundKey(state, roundKeys, 0);

            byte[] output = new byte[16];
            for (int col = 0; col < 4; col++)
            {
                for (int row = 0; row < 4; row++)
                {
                    output[col * 4 + row] = state[row, col];
                }
            }
            return output;
        }

        private static void AddRoundKey(byte[,] state, uint[] roundKeys, int round)
        {
            for (int col = 0; col < 4; col++)
            {
                uint word = roundKeys[round * 4 + col];
                state[0, col] ^= (byte)((word >> 24) & 0xFF);
                state[1, col] ^= (byte)((word >> 16) & 0xFF);
                state[2, col] ^= (byte)((word >> 8) & 0xFF);
                state[3, col] ^= (byte)(word & 0xFF);
            }
        }

        private static void InvSubBytes(byte[,] state)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    state[row, col] = InvSBox[state[row, col]];
                }
            }
        }

        /// <summary>
        /// Inverse ShiftRows: row r is cyclically rotated right by r positions.
        /// </summary>
        private static void InvShiftRows(byte[,] state)
        {
            // Row 0: no shift.

            // Row 1: rotate right by 1.
            byte temp = state[1, 3];
            state[1, 3] = state[1, 2];
            state[1, 2] = state[1, 1];
            state[1, 1] = state[1, 0];
            state[1, 0] = temp;

            // Row 2: rotate right by 2 (swap pairs).
            temp = state[2, 0];
            state[2, 0] = state[2, 2];
            state[2, 2] = temp;
            temp = state[2, 1];
            state[2, 1] = state[2, 3];
            state[2, 3] = temp;

            // Row 3: rotate right by 3 (= rotate left by 1).
            temp = state[3, 0];
            state[3, 0] = state[3, 1];
            state[3, 1] = state[3, 2];
            state[3, 2] = state[3, 3];
            state[3, 3] = temp;
        }

        /// <summary>
        /// Inverse MixColumns using the inverse MDS matrix over GF(2^8).
        /// Coefficients: {0e, 0b, 0d, 09} per column.
        /// </summary>
        private static void InvMixColumns(byte[,] state)
        {
            for (int col = 0; col < 4; col++)
            {
                byte s0 = state[0, col];
                byte s1 = state[1, col];
                byte s2 = state[2, col];
                byte s3 = state[3, col];

                state[0, col] = (byte)(GfMul(0x0e, s0) ^ GfMul(0x0b, s1) ^ GfMul(0x0d, s2) ^ GfMul(0x09, s3));
                state[1, col] = (byte)(GfMul(0x09, s0) ^ GfMul(0x0e, s1) ^ GfMul(0x0b, s2) ^ GfMul(0x0d, s3));
                state[2, col] = (byte)(GfMul(0x0d, s0) ^ GfMul(0x09, s1) ^ GfMul(0x0e, s2) ^ GfMul(0x0b, s3));
                state[3, col] = (byte)(GfMul(0x0b, s0) ^ GfMul(0x0d, s1) ^ GfMul(0x09, s2) ^ GfMul(0x0e, s3));
            }
        }

        /// <summary>
        /// Galois Field multiplication in GF(2^8) with irreducible polynomial
        /// x^8 + x^4 + x^3 + x + 1 (0x11b), using the Russian peasant algorithm.
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
        private static uint RotWord(uint w)
        {
            return (w << 8) | (w >> 24);
        }

        /// <summary>Substitutes each byte of a 32-bit word through the AES S-box (used in key schedule).</summary>
        private static uint SubWord(uint w)
        {
            return ((uint)SBox[(w >> 24) & 0xFF] << 24) |
                   ((uint)SBox[(w >> 16) & 0xFF] << 16) |
                   ((uint)SBox[(w >> 8) & 0xFF] << 8) |
                   (uint)SBox[w & 0xFF];
        }
    }
}
