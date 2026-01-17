using System;

namespace PdfRender.Fonts.Type1;

/// <summary>
/// Provides Type1 font decryption utilities for eexec sections and CharStrings.
/// Implements the standard Type1 cipher per Adobe specification.
/// </summary>
internal static class Type1Decryptor
{
    private const int C1 = 52845;
    private const int C2 = 22719;
    private const int EexecSeed = 55665;
    private const int CharStringSeed = 4330;

    /// <summary>
    /// Decrypt an eexec-encrypted binary segment returning the cleartext bytes
    /// excluding the initial4 random seed bytes per the specification.
    /// </summary>
    /// <param name="encryptedData">Encrypted eexec data.</param>
    /// <returns>Decrypted cleartext span skipping the4-byte seed; may be empty.</returns>
    public static ReadOnlySpan<byte> DecryptEexecBinary(ReadOnlySpan<byte> encryptedData)
    {
        int r = EexecSeed;
        if (encryptedData.Length == 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        byte[] plain = new byte[encryptedData.Length];
        for (int index = 0; index < encryptedData.Length; index++)
        {
            int cipherByte = encryptedData[index];
            int plainByte = cipherByte ^ r >> 8;
            plain[index] = (byte)plainByte;
            r = (cipherByte + r) * C1 + C2 & 0xFFFF;
        }

        if (plain.Length <= 4)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return new ReadOnlySpan<byte>(plain, 4, plain.Length - 4);
    }

    /// <summary>
    /// Decrypt a Type1 CharString program and remove the initial random bytes
    /// determined by the LenIV value (may be zero or &lt;=0 meaning no removal).
    /// </summary>
    /// <param name="encryptedData">Encrypted CharString data.</param>
    /// <param name="lenIV">Random prefix length (LenIV). Values &lt;=0 result in no trimming.</param>
    /// <returns>Decrypted CharString byte array (never null).</returns>
    public static byte[] DecryptCharString(ReadOnlySpan<byte> encryptedData, int lenIV)
    {
        if (encryptedData.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        int r = CharStringSeed;
        byte[] output = new byte[encryptedData.Length];
        for (int index = 0; index < encryptedData.Length; index++)
        {
            int cipherByte = encryptedData[index];
            int plainByte = cipherByte ^ r >> 8;
            output[index] = (byte)plainByte;
            r = (cipherByte + r) * C1 + C2 & 0xFFFF;
        }

        if (lenIV > 0 && output.Length > lenIV)
        {
            int trimmedLength = output.Length - lenIV;
            byte[] trimmed = new byte[trimmedLength];
            Buffer.BlockCopy(output, lenIV, trimmed, 0, trimmedLength);
            return trimmed;
        }

        return output;
    }
}
