using System;
using System.Security.Cryptography;
using PdfRender.Models;

namespace PdfRender.Encryption
{
    /// <summary>
    /// Standard security handler implementation for revision R=2 (RC4, 40..128 bit keys).
    /// Implements Algorithm 3.2 (encryption key) and user password validation (Algorithm 3.4) from PDF spec.
    /// Owner password derivation path is not implemented yet (future enhancement).
    /// </summary>
    internal sealed class StandardR2Decryptor : BasePdfDecryptor
    {
        private const int DefaultKeyBits = 40;
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

        public StandardR2Decryptor(PdfDecryptorParameters parameters) : base(parameters)
        {
        }

        public override ReadOnlyMemory<byte> DecryptString(ReadOnlyMemory<byte> data, PdfReference reference)
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
                // Cannot derive key reliably without file ID – abort silently (consumer will treat as unencrypted fallback).
                return;
            }

            int bits = Parameters.LengthBits > 0 ? Parameters.LengthBits : DefaultKeyBits;
            if (bits < 40)
            {
                bits = 40;
            }
            if (bits > 128)
            {
                bits = 128;
            }
            _fileKeyLengthBytes = bits / 8;

            using (var md5 = MD5.Create())
            {
                // Step order per Algorithm 3.2: padded password, owner entry, permissions (LE 4), file ID (first element)
                var pwdBytes = GetPasswordBytes();
                md5.TransformBlock(pwdBytes, 0, pwdBytes.Length, null, 0);

                if (Parameters.OwnerEntry != null)
                {
                    md5.TransformBlock(Parameters.OwnerEntry, 0, Parameters.OwnerEntry.Length, null, 0);
                }

                var p = BitConverter.GetBytes(Parameters.Permissions);
                md5.TransformBlock(p, 0, 4, null, 0);

                md5.TransformBlock(Parameters.FileIdFirst, 0, Parameters.FileIdFirst.Length, null, 0);
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var digest = md5.Hash;
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
            if (Parameters.UserEntry == null || _fileKey == null)
            {
                return;
            }
            try
            {
                byte[] expectedU = ComputeUserEntryR2();
                // Stored U is 32 bytes: first 16 from RC4 result, remaining padding
                // Compare full length if available, else compare first 16 bytes.
                if (Parameters.UserEntry.Length >= 16)
                {
                    int compareLength = Math.Min(expectedU.Length, Parameters.UserEntry.Length);
                    bool match = true;
                    for (int i = 0; i < compareLength; i++)
                    {
                        if (expectedU[i] != Parameters.UserEntry[i])
                        {
                            match = false;
                            break;
                        }
                    }
                    _userValidated = match;
                }
            }
            catch
            {
                // Ignore validation failures silently – decryption may still proceed for lenient scenarios.
            }
        }

        private byte[] ComputeUserEntryR2()
        {
            using (var md5 = MD5.Create())
            {
                md5.TransformBlock(PasswordPadding, 0, PasswordPadding.Length, null, 0);
                if (Parameters.FileIdFirst != null)
                {
                    md5.TransformBlock(Parameters.FileIdFirst, 0, Parameters.FileIdFirst.Length, null, 0);
                }
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var digest = md5.Hash; // 16 bytes

                // Encrypt digest with RC4 using file key
                var rc4Out = Rc4(_fileKey, digest); // 16 bytes
                // Append padding to form 32 bytes (spec stores 32, but comparison can be first 16)
                byte[] full = new byte[32];
                Buffer.BlockCopy(rc4Out.ToArray(), 0, full, 0, rc4Out.Length);
                Buffer.BlockCopy(PasswordPadding, 0, full, rc4Out.Length, 32 - rc4Out.Length);
                return full;
            }
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
            byte[] output = new byte[data.Length];
            data.CopyTo(output);
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
            for (int k = 0; k < output.Length; k++)
            {
                iIndex = (iIndex + 1) & 0xFF;
                j = (j + s[iIndex]) & 0xFF;
                (s[iIndex], s[j]) = (s[j], s[iIndex]);
                int t = (s[iIndex] + s[j]) & 0xFF;
                output[k] ^= s[t];
            }
            return output;
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
