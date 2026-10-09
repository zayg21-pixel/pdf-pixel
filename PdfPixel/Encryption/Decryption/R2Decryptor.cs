using PdfPixel.Encryption.Cryptography;
using PdfPixel.Encryption.Model;
using System;

namespace PdfPixel.Encryption.Decryption;

/// <summary>
/// Standard security handler implementation for revision R=2 (RC4, 40..128 bit keys).
/// Implements Algorithm 3.2 (encryption key), user password validation (Algorithm 3.4) and
/// owner password validation (Algorithm 3.7) from PDF spec.
/// </summary>
internal sealed class R2Decryptor : BasePdfDecryptor
{
    private const int DefaultKeyBits = 40;

    public R2Decryptor(PdfDecryptorParameters parameters, PdfCredentialRequestedCallback? onCredentialRequested)
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

        byte[] paddedPassword = StandardKeyDerivation.PadPassword(passwordCredential.Password);
        byte[]? fileKey = TryComputeUserFileKey(paddedPassword);
        if (fileKey == null)
        {
            fileKey = TryComputeUserFileKey(RecoverUserPassword(paddedPassword));
        }

        return fileKey;
    }

    /// <summary>
    /// Algorithms 2 and 6: the file key for the padded user password, or null when /U does not match.
    /// </summary>
    private byte[]? TryComputeUserFileKey(byte[] paddedPassword)
    {
        byte[] userEntry = StandardKeyDerivation.GetUserEntry(Parameters);
        int keyLength = StandardKeyDerivation.GetKeyLength(Parameters, DefaultKeyBits);
        byte[] fileKey = StandardKeyDerivation.ComputeKeyDigest(paddedPassword, Parameters).AsSpan(0, keyLength).ToArray();

        var expectedUserEntry = new byte[StandardKeyDerivation.PasswordPadding.Length];
        new Rc4(fileKey).Decrypt(StandardKeyDerivation.PasswordPadding, expectedUserEntry);
        int compareLength = Math.Min(expectedUserEntry.Length, userEntry.Length);
        if (!expectedUserEntry.AsSpan(0, compareLength).SequenceEqual(userEntry.AsSpan(0, compareLength)))
        {
            return null;
        }

        return fileKey;
    }

    /// <summary>
    /// Algorithm 7 step b for R2: the padded user password decrypted from /O with the owner password key.
    /// </summary>
    private byte[] RecoverUserPassword(byte[] paddedOwnerPassword)
    {
        int keyLength = StandardKeyDerivation.GetKeyLength(Parameters, DefaultKeyBits);
        byte[] ownerKey = Md5.ComputeHash(paddedOwnerPassword).AsSpan(0, keyLength).ToArray();
        byte[] ownerEntry = StandardKeyDerivation.GetOwnerEntry(Parameters);
        byte[] paddedUserPassword = ownerEntry.AsSpan(0, Math.Min(ownerEntry.Length, StandardKeyDerivation.PasswordPadding.Length)).ToArray();
        new Rc4(ownerKey).Decrypt(paddedUserPassword, paddedUserPassword);

        return paddedUserPassword;
    }
}
