using System;
using System.Text;
using PdfPixel.Models;

namespace PdfPixel.Encryption;

/// <summary>
/// Decryptor for the Standard security handler revision R=6 (AES-256, ISO 32000-2).
/// Implements the hardened hash (Algorithm 2.B), user/owner password validation, and file key
/// unwrapping (Algorithm 8.1) from the PDF 2.0 specification. Revision R=5 (the deprecated,
/// pre-standardization AES-256 variant from ISO 32000-1 ExtensionLevel 3) is not supported.
/// Object keys for AESV3 are the file encryption key itself; unlike RC4/AESV2, no per-object
/// key derivation is performed.
/// </summary>
internal sealed class R5R6Decryptor : BasePdfDecryptor
{
    private const int MaxPasswordBytes = 127;
    private const int UEntryLength = 48;

    private byte[]? _fileKey;
    private string _lastPassword = string.Empty;
    private readonly ManagedAes256Cbc _aes = new();

    public R5R6Decryptor(PdfDecryptorParameters parameters)
        : base(parameters)
    {
        if (parameters.R != 6)
        {
            // TODO: [MEDIUM] implement R5 (deprecated, pre-standardization AES-256 variant from ISO 32000-1 ExtensionLevel 3)
            throw new NotSupportedException($"PDF Standard Security Handler revision {parameters.R} (deprecated AES-256) is not supported. Only revision 6 is supported.");
        }
    }

    public override byte[] DecryptString(ReadOnlyMemory<byte> data, PdfReference reference)
    {
        if (data.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        EnsureFileKey();

        if (data.Length < 16)
        {
            return data.ToArray();
        }

        ReadOnlySpan<byte> span = data.Span;
        byte[] iv = span.Slice(0, 16).ToArray();
        byte[] ciphertext = span.Slice(16).ToArray();

        if (_fileKey == null)
        {
            throw new InvalidOperationException("File key must be computed before decrypting.");
        }

        return _aes.Decrypt(_fileKey, iv, ciphertext, stripPkcs7Padding: true);
    }

    public override void UpdatePassword(string password)
    {
        base.UpdatePassword(password);
        if (password != _lastPassword)
        {
            _fileKey = null;
            _lastPassword = password;
        }

        EnsureFileKey();
    }

    private void EnsureFileKey()
    {
        if (_fileKey != null)
        {
            return;
        }

        byte[] userEntry = Parameters.UserEntry ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /U (user entry).");
        byte[] ownerEntry = Parameters.OwnerEntry ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /O (owner entry).");
        byte[] userEncryptedKey = Parameters.UserEncryptedKey ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /UE (user encrypted key) entry.");
        byte[] ownerEncryptedKey = Parameters.OwnerEncryptedKey ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /OE (owner encrypted key) entry.");

        if (userEntry.Length < UEntryLength)
        {
            throw new PdfInvalidDocumentException("Encrypted document has a malformed /U (user entry); expected at least 48 bytes.");
        }

        // Algorithm 2.A/2.B require exactly the 48-byte U string when hashing the owner password.
        // Some writers pad /U with trailing bytes beyond the required 48; only the first 48 are significant.
        byte[] uString = userEntry.AsSpan(0, UEntryLength).ToArray();

        byte[] passwordBytes = GetPasswordBytes();
        var zeroIv = new byte[16];

        byte[] userValidationSalt = userEntry.AsSpan(32, 8).ToArray();
        byte[] userHash = Hash2B(passwordBytes, userValidationSalt, userKey: null);
        if (userHash.AsSpan().SequenceEqual(userEntry.AsSpan(0, 32)))
        {
            byte[] userKeySalt = userEntry.AsSpan(40, 8).ToArray();
            byte[] intermediateKey = Hash2B(passwordBytes, userKeySalt, userKey: null);
            _fileKey = _aes.Decrypt(intermediateKey, zeroIv, userEncryptedKey, stripPkcs7Padding: false);
            return;
        }

        byte[] ownerValidationSalt = ownerEntry.AsSpan(32, 8).ToArray();
        byte[] ownerHash = Hash2B(passwordBytes, ownerValidationSalt, uString);
        if (ownerHash.AsSpan().SequenceEqual(ownerEntry.AsSpan(0, 32)))
        {
            byte[] ownerKeySalt = ownerEntry.AsSpan(40, 8).ToArray();
            byte[] intermediateKey = Hash2B(passwordBytes, ownerKeySalt, uString);
            _fileKey = _aes.Decrypt(intermediateKey, zeroIv, ownerEncryptedKey, stripPkcs7Padding: false);
            return;
        }

        throw new PdfIncorrectPasswordException();
    }

    /// <summary>
    /// Implements ISO 32000-2 Algorithm 2.B (the R6 hardened hash).
    /// </summary>
    private static byte[] Hash2B(byte[] password, byte[] salt, byte[]? userKey)
    {
        byte[] k = ManagedSha256.ComputeHash(Concat(password, salt, userKey));
        ManagedAes128CbcEncryptor aesEncryptor = new();

        int round = 0;
        while (true)
        {
            byte[] k1 = RepeatConcat(password, k, userKey);
            byte[] aesKey = k.AsSpan(0, 16).ToArray();
            byte[] aesIv = k.AsSpan(16, 16).ToArray();
            byte[] e = aesEncryptor.Encrypt(aesKey, aesIv, k1);

            int sum = 0;
            for (int i = 0; i < 16; i++)
            {
                sum += e[i];
            }

            k = (sum % 3) switch
            {
                0 => ManagedSha256.ComputeHash(e),
                1 => ManagedSha512.ComputeHash384(e),
                _ => ManagedSha512.ComputeHash512(e)
            };

            round++;
            if (round >= 64 && e[e.Length - 1] <= round - 32)
            {
                break;
            }
        }

        return k.AsSpan(0, 32).ToArray();
    }

    private static byte[] Concat(byte[] first, byte[] second, byte[]? third)
    {
        var result = new byte[first.Length + second.Length + (third?.Length ?? 0)];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        if (third != null)
        {
            Buffer.BlockCopy(third, 0, result, first.Length + second.Length, third.Length);
        }

        return result;
    }

    private static byte[] RepeatConcat(byte[] password, byte[] k, byte[]? userKey)
    {
        int unitLength = password.Length + k.Length + (userKey?.Length ?? 0);
        var result = new byte[unitLength * 64];
        int offset = 0;
        for (int repetition = 0; repetition < 64; repetition++)
        {
            Buffer.BlockCopy(password, 0, result, offset, password.Length);
            offset += password.Length;
            Buffer.BlockCopy(k, 0, result, offset, k.Length);
            offset += k.Length;
            if (userKey != null)
            {
                Buffer.BlockCopy(userKey, 0, result, offset, userKey.Length);
                offset += userKey.Length;
            }
        }

        return result;
    }

    private byte[] GetPasswordBytes()
    {
        string password = Password ?? string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        if (bytes.Length <= MaxPasswordBytes)
        {
            return bytes;
        }

        var truncated = new byte[MaxPasswordBytes];
        Buffer.BlockCopy(bytes, 0, truncated, 0, MaxPasswordBytes);
        return truncated;
    }
}
