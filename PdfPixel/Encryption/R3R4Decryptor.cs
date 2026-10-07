using PdfPixel.Encryption.Cryptography;
using System;

namespace PdfPixel.Encryption;

/// <summary>
/// Standard security handler implementation for revisions R=3 and R=4 (RC4 or AESV2, 40..128 bit keys).
/// </summary>
internal sealed class R3R4Decryptor : BasePdfDecryptor
{
    private const int DefaultKeyBits = 128;
    private const int KeyHashIterations = 50;
    private const int UserEntryRounds = 20;
    private const int UserEntryCompareLength = 16;
    private const int MinV4KeyLength = 16;

    public R3R4Decryptor(PdfDecryptorParameters parameters, PdfPasswordRequestedCallback? onPasswordRequested)
        : base(parameters, onPasswordRequested)
    {
    }

    /// <inheritdoc />
    protected override byte[]? TryComputeFileKey(string password)
    {
        byte[] userEntry = StandardKeyDerivation.GetUserEntry(Parameters);
        int keyLength = StandardKeyDerivation.GetKeyLength(Parameters, DefaultKeyBits);

        byte[] digest = StandardKeyDerivation.ComputeKeyDigest(password, Parameters);
        for (int i = 0; i < KeyHashIterations; i++)
        {
            digest = Md5.ComputeHash(digest.AsSpan(0, keyLength));
        }

        byte[] fileKey = digest.AsSpan(0, keyLength).ToArray();
        if (!ComputeUserEntry(fileKey).AsSpan().SequenceEqual(userEntry.AsSpan(0, UserEntryCompareLength)))
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
        for (int round = 0; round < UserEntryRounds; round++)
        {
            for (int i = 0; i < fileKey.Length; i++)
            {
                roundKey[i] = (byte)(fileKey[i] ^ round);
            }

            block = Rc4.Transform(roundKey, block);
        }

        return block;
    }
}
