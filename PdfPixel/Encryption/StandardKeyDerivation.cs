using PdfPixel.Encryption.Cryptography;
using System;
using System.Buffers.Binary;
using System.Text;

namespace PdfPixel.Encryption;

/// <summary>
/// Shared steps of the Standard security handler key derivation for revisions 2 to 4 (ISO 32000-2, 7.6.4.3).
/// </summary>
internal static class StandardKeyDerivation
{
    private const int PasswordPadLength = 32;
    private const int MinKeyBits = 40;
    private const int MaxKeyBits = 128;
    private const int MinUserEntryLength = 16;

    /// <summary>
    /// Password padding string (ISO 32000-2, Algorithm 2 step a).
    /// </summary>
    public static readonly byte[] PasswordPadding = [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];

    /// <summary>
    /// Returns the file key length in bytes from /Length, or from <paramref name="defaultBits"/> when absent.
    /// </summary>
    public static int GetKeyLength(PdfDecryptorParameters parameters, int defaultBits)
    {
        int bits = (parameters.LengthBits == 0) ? defaultBits : parameters.LengthBits;
        return Math.Max(MinKeyBits, Math.Min(MaxKeyBits, bits)) / 8;
    }

    /// <summary>
    /// Algorithm 2 steps a to f: MD5 of the padded password, /O, /P, the first /ID entry and,
    /// for R4 with unencrypted metadata, 0xFFFFFFFF.
    /// </summary>
    public static byte[] ComputeKeyDigest(string password, PdfDecryptorParameters parameters)
    {
        byte[] fileId = GetFileId(parameters);
        byte[] ownerEntry = parameters.OwnerEntry ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /O (owner entry).");
        bool appendMetadataMarker = parameters.R >= 4 && !parameters.EncryptMetadata;

        var input = new byte[PasswordPadLength + ownerEntry.Length + 4 + fileId.Length + (appendMetadataMarker ? 4 : 0)];
        byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
        int passwordLength = Math.Min(passwordBytes.Length, PasswordPadLength);
        passwordBytes.AsSpan(0, passwordLength).CopyTo(input);
        PasswordPadding.AsSpan(0, PasswordPadLength - passwordLength).CopyTo(input.AsSpan(passwordLength));

        Span<byte> remaining = input.AsSpan(PasswordPadLength);
        ownerEntry.CopyTo(remaining);
        remaining = remaining.Slice(ownerEntry.Length);
        BinaryPrimitives.WriteInt32LittleEndian(remaining, parameters.Permissions);
        remaining = remaining.Slice(4);
        fileId.CopyTo(remaining);
        remaining = remaining.Slice(fileId.Length);

        if (appendMetadataMarker)
        {
            BinaryPrimitives.WriteInt32LittleEndian(remaining, -1);
        }

        return Md5.ComputeHash(input);
    }

    /// <summary>
    /// Returns the first /ID entry.
    /// </summary>
    public static byte[] GetFileId(PdfDecryptorParameters parameters)
        => parameters.FileIdFirst ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /ID first entry.");

    /// <summary>
    /// Returns the /U entry.
    /// </summary>
    public static byte[] GetUserEntry(PdfDecryptorParameters parameters)
    {
        byte[] userEntry = parameters.UserEntry ?? throw new PdfInvalidDocumentException("Encrypted document is missing the required /U (user entry).");
        if (userEntry.Length < MinUserEntryLength)
        {
            throw new PdfInvalidDocumentException("Encrypted document /U entry is too short to validate.");
        }

        return userEntry;
    }
}
