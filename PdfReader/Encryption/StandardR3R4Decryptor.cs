using System;
using System.Security.Cryptography;
using PdfReader.Models;

namespace PdfReader.Encryption
{
    /// <summary>
    /// Standard security handler implementation for revisions R=3 and R=4 (legacy RC4 mode only here).
    /// Implements Algorithm 3.2 (file key derivation, including /O and optional metadata flag)
    /// and Algorithm 3.5 (user password validation RC4 20-iteration loop) per PDF specification.
    /// AESV2 crypt filter support is not implemented yet.
    /// </summary>
    internal sealed class StandardR3R4Decryptor : BasePdfDecryptor
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

        public StandardR3R4Decryptor(PdfDecryptorParameters parameters) : base(parameters)
        {
        }

        public override ReadOnlyMemory<byte> DecryptBytes(ReadOnlyMemory<byte> data, PdfReference reference)
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
            var objectKey = DeriveObjectKey(reference);
            return Rc4(objectKey, data.Span);
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

        private void EnsureFileKey()
        {
            if (_fileKey != null)
            {
                return;
            }

            if (Parameters.FileIdFirst == null)
            {
                // Cannot derive key reliably without file ID.
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
                // Order (Algorithm 3.2): padded password, owner entry (/O), permissions (LE 4), file ID (first), optional 0xFFFFFFFF if !EncryptMetadata
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

                // Strengthening loop: hash first key length bytes 50 times.
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
                // Safe to ignore; decryption may still proceed for lenient consumers.
            }
        }

        private byte[] ComputeUserEntryR3R4()
        {
            // Algorithm 3.5: MD5(padding + fileID), then 20 iterative RC4 encryptions with fileKey XOR i.
            byte[] digest;
            using (var md5 = MD5.Create())
            {
                md5.TransformBlock(PasswordPadding, 0, PasswordPadding.Length, null, 0);
                md5.TransformBlock(Parameters.FileIdFirst, 0, Parameters.FileIdFirst.Length, null, 0);
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                digest = md5.Hash; // 16 bytes
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

            return block; // Compare first 16 bytes only
        }

        private byte[] DeriveObjectKey(PdfReference reference)
        {
            Span<byte> buffer = stackalloc byte[_fileKeyLengthBytes + 5];
            _fileKey.AsSpan(0, _fileKeyLengthBytes).CopyTo(buffer);
            uint obj = reference.ObjectNumber;
            int gen = reference.Generation;
            buffer[_fileKeyLengthBytes + 0] = (byte)(obj & 0xFF);
            buffer[_fileKeyLengthBytes + 1] = (byte)((obj >> 8) & 0xFF);
            buffer[_fileKeyLengthBytes + 2] = (byte)((obj >> 16) & 0xFF);
            buffer[_fileKeyLengthBytes + 3] = (byte)(gen & 0xFF);
            buffer[_fileKeyLengthBytes + 4] = (byte)((gen >> 8) & 0xFF);

            using (var md5 = MD5.Create())
            {
                var digest = md5.ComputeHash(buffer.ToArray());
                int keyLen = _fileKeyLengthBytes + 5;
                if (keyLen > 16)
                {
                    keyLen = 16;
                }
                var objectKey = new byte[keyLen];
                Buffer.BlockCopy(digest, 0, objectKey, 0, keyLen);
                return objectKey;
            }
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
    }
}
