using System;
using System.IO;
using System.Security.Cryptography;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Encryption
{
    /// <summary>
    /// Unified decryptor for Standard security handler revisions R=3 and R=4.
    /// Implements common key derivation and supports RC4 (V2) and AESV2 per crypt filter method.
    /// Uses distinct paths for streams and strings honoring CF overrides.
    /// </summary>
    internal sealed class R3R4Decryptor : BasePdfDecryptor
    {
        private const int PasswordPadLength = 32;
        private static readonly byte[] PasswordPadding = new byte[]
        {
            0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
            0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
            0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
            0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
        };

        private byte[] _fileKey;
        private int _fileKeyLengthBytes;
        private string _lastPassword = string.Empty;
        private bool _userValidated;

        public R3R4Decryptor(PdfDecryptorParameters parameters) : base(parameters)
        {
        }

        public override ReadOnlyMemory<byte> DecryptString(ReadOnlyMemory<byte> data, PdfReference reference)
        {
            // Treat as string path
            return DecryptInternal(data, reference, useStreamPath: false);
        }

        public override Stream DecryptStream(Stream stream, PdfReference reference)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var decryptedBytes = DecryptInternal(memoryStream.ToArray(), reference, useStreamPath: true);
            return new MemoryStream(decryptedBytes.ToArray());
        }

        private ReadOnlyMemory<byte> DecryptInternal(ReadOnlyMemory<byte> data, PdfReference reference, bool useStreamPath)
        {
            if (data.IsEmpty)
            {
                return data;
            }

            EnsureFileKey();
            if (_fileKey == null)
            {
                return data;
            }

            // Choose CF method and length override
            PdfString method = useStreamPath ? Parameters.StreamCryptFilterMethod : Parameters.StringCryptFilterMethod;
            int? overrideLenBytes = useStreamPath ? Parameters.StreamCryptFilterLength : Parameters.StringCryptFilterLength;
            bool useAes = method == PdfTokens.AESV2;

            var objectKey = DeriveObjectKey(reference, useAes, overrideLenBytes);

            if (useAes)
            {
                return AesV2(objectKey, data.Span);
            }

            // Default RC4
            return Rc4(objectKey, data.Span);
        }

        private void EnsureFileKey()
        {
            if (_fileKey != null)
            {
                return;
            }

            if (Parameters.FileIdFirst == null)
            {
                return;
            }

            int bits = Parameters.LengthBits;
            if (bits < 40)
            {
                bits = 40;
            }
            if (bits > 128)
            {
                bits = 128;
            }
            if (bits == 0)
            {
                bits = 128;
            }
            _fileKeyLengthBytes = bits / 8;

            using (var md5 = MD5.Create())
            {
                var pwdBytes = GetPasswordBytes();
                md5.TransformBlock(pwdBytes, 0, pwdBytes.Length, null, 0);

                if (Parameters.OwnerEntry != null)
                {
                    md5.TransformBlock(Parameters.OwnerEntry, 0, Parameters.OwnerEntry.Length, null, 0);
                }

                var p = BitConverter.GetBytes(Parameters.Permissions);
                md5.TransformBlock(p, 0, 4, null, 0);

                md5.TransformBlock(Parameters.FileIdFirst, 0, Parameters.FileIdFirst.Length, null, 0);

                if (!Parameters.EncryptMetadata)
                {
                    byte[] meta = { 0xFF, 0xFF, 0xFF, 0xFF };
                    md5.TransformBlock(meta, 0, 4, null, 0);
                }

                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                byte[] digest = md5.Hash;

                for (int i = 0; i < 50; i++)
                {
                    digest = md5.ComputeHash(digest.AsSpan(0, _fileKeyLengthBytes).ToArray());
                }

                _fileKey = new byte[_fileKeyLengthBytes];
                Buffer.BlockCopy(digest, 0, _fileKey, 0, _fileKeyLengthBytes);
            }

            ValidateUserPassword();
        }

        private void ValidateUserPassword()
        {
            if (_userValidated)
            {
                return;
            }
            if (_fileKey == null || Parameters.UserEntry == null || Parameters.UserEntry.Length < 16)
            {
                return;
            }

            try
            {
                byte[] expectedFirst16 = ComputeUserEntryR3R4();
                bool match = true;
                for (int i = 0; i < 16; i++)
                {
                    if (expectedFirst16[i] != Parameters.UserEntry[i])
                    {
                        match = false;
                        break;
                    }
                }
                _userValidated = match;
            }
            catch
            {
                // Intentionally ignored; some producers accept decryption without strict validation.
            }
        }

        private byte[] ComputeUserEntryR3R4()
        {
            byte[] digest;
            using (var md5 = MD5.Create())
            {
                md5.TransformBlock(PasswordPadding, 0, PasswordPadding.Length, null, 0);
                md5.TransformBlock(Parameters.FileIdFirst, 0, Parameters.FileIdFirst.Length, null, 0);
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                digest = md5.Hash;
            }

            byte[] block = new byte[16];
            Buffer.BlockCopy(digest, 0, block, 0, 16);

            byte[] tempKey = new byte[_fileKey.Length];
            for (int i = 0; i < 20; i++)
            {
                for (int k = 0; k < _fileKey.Length; k++)
                {
                    tempKey[k] = (byte)(_fileKey[k] ^ i);
                }
                block = Rc4Raw(tempKey, block);
            }

            return block;
        }

        private byte[] DeriveObjectKey(PdfReference reference, bool useAes, int? cryptFilterKeyLengthBytesOverride = null)
        {
            Span<byte> buffer = stackalloc byte[_fileKeyLengthBytes + 5 + (useAes ? 4 : 0)];
            _fileKey.AsSpan(0, _fileKeyLengthBytes).CopyTo(buffer);
            uint obj = reference.ObjectNumber;
            int gen = reference.Generation;
            buffer[_fileKeyLengthBytes + 0] = (byte)(obj & 0xFF);
            buffer[_fileKeyLengthBytes + 1] = (byte)((obj >> 8) & 0xFF);
            buffer[_fileKeyLengthBytes + 2] = (byte)((obj >> 16) & 0xFF);
            buffer[_fileKeyLengthBytes + 3] = (byte)(gen & 0xFF);
            buffer[_fileKeyLengthBytes + 4] = (byte)((gen >> 8) & 0xFF);

            if (useAes)
            {
                buffer[_fileKeyLengthBytes + 5] = (byte)'s';
                buffer[_fileKeyLengthBytes + 6] = (byte)'A';
                buffer[_fileKeyLengthBytes + 7] = (byte)'l';
                buffer[_fileKeyLengthBytes + 8] = (byte)'T';
            }

            using var md5 = MD5.Create();

            var digest = md5.ComputeHash(buffer.ToArray());
            int baseLen = cryptFilterKeyLengthBytesOverride ?? _fileKeyLengthBytes;
            int keyLen = baseLen + 5;
            if (keyLen > 16)
            {
                keyLen = 16;
            }
            var objectKey = new byte[keyLen];
            Buffer.BlockCopy(digest, 0, objectKey, 0, keyLen);
            return objectKey;
        }

        private static ReadOnlyMemory<byte> Rc4(byte[] key, ReadOnlySpan<byte> data)
        {
            byte[] output = data.ToArray();
            Rc4InPlace(key, output);
            return output;
        }

        private static byte[] Rc4Raw(byte[] key, byte[] block)
        {
            byte[] copy = new byte[block.Length];
            Buffer.BlockCopy(block, 0, copy, 0, block.Length);
            Rc4InPlace(key, copy);
            return copy;
        }

        private static void Rc4InPlace(byte[] key, byte[] buffer)
        {
            Span<byte> s = stackalloc byte[256];
            for (int i = 0; i < 256; i++)
            {
                s[i] = (byte)i;
            }
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) & 0xFF;
                (s[i], s[j]) = (s[j], s[i]);
            }
            int iIndex = 0;
            j = 0;
            for (int k = 0; k < buffer.Length; k++)
            {
                iIndex = (iIndex + 1) & 0xFF;
                j = (j + s[iIndex]) & 0xFF;
                (s[iIndex], s[j]) = (s[j], s[iIndex]);
                int t = (s[iIndex] + s[j]) & 0xFF;
                buffer[k] ^= s[t];
            }
        }

        private static ReadOnlyMemory<byte> AesV2(byte[] objectKey, ReadOnlySpan<byte> data)
        {
            if (data.Length < 16)
            {
                return data.ToArray();
            }

            byte[] iv = data.Slice(0, 16).ToArray();
            byte[] ciphertext = data.Slice(16).ToArray();

            using var aes = Aes.Create();
            byte[] key = objectKey.Length >= 16 ? objectKey.AsSpan(0, 16).ToArray() : PadKeyTo16(objectKey);
            using ICryptoTransform decryptor = aes.CreateDecryptor(key, iv);
            byte[] plain = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return plain;
        }

        private static byte[] PadKeyTo16(byte[] key)
        {
            byte[] padded = new byte[16];
            int copy = Math.Min(16, key.Length);
            Buffer.BlockCopy(key, 0, padded, 0, copy);
            return padded;
        }

        private byte[] GetPasswordBytes()
        {
            string pwd = Password ?? string.Empty;
            var bytes = System.Text.Encoding.ASCII.GetBytes(pwd);
            if (bytes.Length > PasswordPadLength)
            {
                byte[] trimmed = new byte[PasswordPadLength];
                Buffer.BlockCopy(bytes, 0, trimmed, 0, PasswordPadLength);
                return trimmed;
            }
            byte[] padded = new byte[PasswordPadLength];
            Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
            Buffer.BlockCopy(PasswordPadding, 0, padded, bytes.Length, PasswordPadLength - bytes.Length);
            return padded;
        }

        public override void UpdatePassword(string password)
        {
            base.UpdatePassword(password);
            if (password != _lastPassword)
            {
                _fileKey = null;
                _userValidated = false;
                _lastPassword = password;
            }
        }
    }
}
