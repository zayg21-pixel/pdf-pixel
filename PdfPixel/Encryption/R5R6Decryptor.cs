using System;
using System.Text;
using PdfPixel.Encryption.Cryptography;

namespace PdfPixel.Encryption;

/// <summary>
/// Decryptor for the Standard security handler revisions R=5 and R=6 (AES-256).
/// Implements the hardened hash (Algorithm 2.B), user/owner password validation, and file key
/// unwrapping (Algorithm 8.1) from the PDF 2.0 specification. Revision R=5 (the deprecated,
/// pre-standardization AES-256 variant from ISO 32000-1 ExtensionLevel 3) uses plain SHA-256
/// in place of Algorithm 2.B.
/// Object keys for AESV3 are the file encryption key itself; unlike RC4/AESV2, no per-object
/// key derivation is performed.
/// </summary>
internal sealed class R5R6Decryptor : BasePdfDecryptor
{
    private const int MaxPasswordBytes = 127;
    private const int UEntryLength = 48;

    public R5R6Decryptor(PdfDecryptorParameters parameters, PdfCredentialRequestedCallback? onCredentialRequested)
        : base(parameters, onCredentialRequested)
    {
    }

    /// <inheritdoc />
    protected override byte[]? TryComputeFileKey(PdfCredential credential)
    {
        if (credential is not PdfPasswordCredential passwordCredential)
        {
            return null;
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

        byte[] passwordBytes = GetPasswordBytes(passwordCredential.Password);
        var zeroIv = new byte[16];

        byte[] userValidationSalt = userEntry.AsSpan(32, 8).ToArray();
        byte[] userHash = ComputeHash(passwordBytes, userValidationSalt, userKey: null);
        if (userHash.AsSpan().SequenceEqual(userEntry.AsSpan(0, 32)))
        {
            byte[] userKeySalt = userEntry.AsSpan(40, 8).ToArray();
            byte[] intermediateKey = ComputeHash(passwordBytes, userKeySalt, userKey: null);
            return AesCbc.Decrypt(intermediateKey, zeroIv, userEncryptedKey, stripPkcs7Padding: false);
        }

        byte[] ownerValidationSalt = ownerEntry.AsSpan(32, 8).ToArray();
        byte[] ownerHash = ComputeHash(passwordBytes, ownerValidationSalt, uString);
        if (ownerHash.AsSpan().SequenceEqual(ownerEntry.AsSpan(0, 32)))
        {
            byte[] ownerKeySalt = ownerEntry.AsSpan(40, 8).ToArray();
            byte[] intermediateKey = ComputeHash(passwordBytes, ownerKeySalt, uString);
            return AesCbc.Decrypt(intermediateKey, zeroIv, ownerEncryptedKey, stripPkcs7Padding: false);
        }

        return null;
    }

    /// <summary>
    /// Password hash: SHA-256 for R5 (Adobe Extension Level 3), Algorithm 2.B for R6.
    /// </summary>
    private byte[] ComputeHash(byte[] password, byte[] salt, byte[]? userKey)
        => (Parameters.R == 5) ? Sha256.ComputeHash(Concat(password, salt, userKey)) : Hash2B(password, salt, userKey);

    /// <summary>
    /// Implements ISO 32000-2 Algorithm 2.B (the R6 hardened hash).
    /// </summary>
    private static byte[] Hash2B(byte[] password, byte[] salt, byte[]? userKey)
    {
        byte[] k = Sha256.ComputeHash(Concat(password, salt, userKey));

        int round = 0;
        while (true)
        {
            byte[] k1 = RepeatConcat(password, k, userKey);
            byte[] e = AesCbc.Encrypt(k.AsSpan(0, 16), k.AsSpan(16, 16), k1);

            int sum = 0;
            for (int i = 0; i < 16; i++)
            {
                sum += e[i];
            }

            k = (sum % 3) switch
            {
                0 => Sha256.ComputeHash(e),
                1 => Sha512.ComputeHash384(e),
                _ => Sha512.ComputeHash512(e)
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

    private static byte[] GetPasswordBytes(string password)
    {
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
