using PdfPixel.Encryption.Cryptography;
using System;

namespace PdfPixel.Encryption;

/// <summary>
/// Standard security handler implementation for revision R=2 (RC4, 40..128 bit keys).
/// Implements Algorithm 3.2 (encryption key) and user password validation (Algorithm 3.4) from PDF spec.
/// Owner password derivation path is not implemented yet (future enhancement).
/// </summary>
internal sealed class R2Decryptor : BasePdfDecryptor
{
    private const int DefaultKeyBits = 40;

    public R2Decryptor(PdfDecryptorParameters parameters, PdfPasswordRequestedCallback? onPasswordRequested)
        : base(parameters, onPasswordRequested)
    {
    }

    /// <inheritdoc />
    protected override byte[]? TryComputeFileKey(string password)
    {
        byte[] userEntry = StandardKeyDerivation.GetUserEntry(Parameters);
        int keyLength = StandardKeyDerivation.GetKeyLength(Parameters, DefaultKeyBits);
        byte[] fileKey = StandardKeyDerivation.ComputeKeyDigest(password, Parameters).AsSpan(0, keyLength).ToArray();

        byte[] expectedUserEntry = Rc4.Transform(fileKey, StandardKeyDerivation.PasswordPadding);
        int compareLength = Math.Min(expectedUserEntry.Length, userEntry.Length);
        if (!expectedUserEntry.AsSpan(0, compareLength).SequenceEqual(userEntry.AsSpan(0, compareLength)))
        {
            return null;
        }

        return fileKey;
    }
}
