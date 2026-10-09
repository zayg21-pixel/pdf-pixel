using PdfPixel.Encryption.Cryptography;
using PdfPixel.Encryption.Model;
using System;

namespace PdfPixel.Encryption.Decryption;

/// <summary>
/// Standard security handler implementation for revisions R=3 and R=4 (RC4 or AESV2, 40..128 bit keys).
/// </summary>
internal sealed class R3R4Decryptor : BasePdfDecryptor
{
    private const int DefaultKeyBits = 128;
    private const int KeyHashIterations = 50;
    private const int Rc4Rounds = 20;
    private const int UserEntryCompareLength = 16;
    private const int MinV4KeyLength = 16;

    public R3R4Decryptor(PdfDecryptorParameters parameters, PdfCredentialRequestedCallback? onCredentialRequested)
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

        if (fileKey == null)
        {
            return null;
        }

        if (Parameters.V == 4 && fileKey.Length < MinV4KeyLength)
        {
            // Undocumented: Acrobat zero-pads V4 file keys shorter than 16 bytes.
            var paddedKey = new byte[MinV4KeyLength];
            fileKey.CopyTo(paddedKey, 0);
            return paddedKey;
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
        byte[] fileKey = Rehash(StandardKeyDerivation.ComputeKeyDigest(paddedPassword, Parameters), keyLength);
        if (!ComputeUserEntry(fileKey).AsSpan().SequenceEqual(userEntry.AsSpan(0, UserEntryCompareLength)))
        {
            return null;
        }

        return fileKey;
    }

    /// <summary>
    /// Algorithm 5 steps a to e: the first 16 bytes of the /U entry for <paramref name="fileKey"/>.
    /// </summary>
    private byte[] ComputeUserEntry(byte[] fileKey)
    {
        byte[] fileId = StandardKeyDerivation.GetFileId(Parameters);
        var input = new byte[StandardKeyDerivation.PasswordPadding.Length + fileId.Length];
        StandardKeyDerivation.PasswordPadding.CopyTo(input, 0);
        fileId.CopyTo(input, StandardKeyDerivation.PasswordPadding.Length);

        byte[] block = Md5.ComputeHash(input);
        var roundKey = new byte[fileKey.Length];
        for (int round = 0; round < Rc4Rounds; round++)
        {
            for (int i = 0; i < fileKey.Length; i++)
            {
                roundKey[i] = (byte)(fileKey[i] ^ round);
            }

            new Rc4(roundKey).Decrypt(block, block);
        }

        return block;
    }

    /// <summary>
    /// Algorithm 7 step b for R3 and R4: the padded user password decrypted from /O with the owner password key.
    /// </summary>
    private byte[] RecoverUserPassword(byte[] paddedOwnerPassword)
    {
        int keyLength = StandardKeyDerivation.GetKeyLength(Parameters, DefaultKeyBits);
        byte[] ownerKey = Rehash(Md5.ComputeHash(paddedOwnerPassword), keyLength);
        byte[] ownerEntry = StandardKeyDerivation.GetOwnerEntry(Parameters);

        byte[] block = ownerEntry.AsSpan(0, Math.Min(ownerEntry.Length, StandardKeyDerivation.PasswordPadding.Length)).ToArray();
        var roundKey = new byte[keyLength];
        for (int round = Rc4Rounds - 1; round >= 0; round--)
        {
            for (int i = 0; i < keyLength; i++)
            {
                roundKey[i] = (byte)(ownerKey[i] ^ round);
            }

            new Rc4(roundKey).Decrypt(block, block);
        }

        return block;
    }

    /// <summary>
    /// Algorithm 2 step h: 50 MD5 rounds over the first <paramref name="keyLength"/> bytes, truncated to that length.
    /// </summary>
    private static byte[] Rehash(byte[] digest, int keyLength)
    {
        for (int i = 0; i < KeyHashIterations; i++)
        {
            digest = Md5.ComputeHash(digest.AsSpan(0, keyLength));
        }

        return digest.AsSpan(0, keyLength).ToArray();
    }
}
