using System;

namespace PdfPixel.Fonts.Type1;

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
    public static ReadOnlySpan<byte> DecryptEexecBinary(in ReadOnlySpan<byte> encryptedData)
    {
        int r = EexecSeed;
        if (encryptedData.Length == 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        var plain = new byte[encryptedData.Length];
        for (int index = 0; index < encryptedData.Length; index++)
        {
            int cipherByte = encryptedData[index];
            int plainByte = cipherByte ^ r >> 8;
            plain[index] = (byte)plainByte;
            r = ((cipherByte + r) * C1) + C2 & 0xFFFF;
        }

        if (plain.Length <= 4)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return new ReadOnlySpan<byte>(plain, 4, plain.Length - 4);
    }

    /// <summary>
    /// Determines whether an eexec-encrypted segment is ASCII-hex encoded rather than raw binary:
    /// its first byte may be whitespace, and the following seven bytes must all be hex digits.
    /// </summary>
    /// <param name="encryptedData">Encrypted eexec data.</param>
    /// <returns>True if the segment looks like an ASCII-hex encoding of the eexec data.</returns>
    public static bool IsAsciiHexEncoded(in ReadOnlySpan<byte> encryptedData)
    {
        const int windowLength = 8;

        if (encryptedData.Length < windowLength)
        {
            return false;
        }

        if (!IsHexDigit(encryptedData[0]) && !IsWhitespace(encryptedData[0]))
        {
            return false;
        }

        for (int index = 1; index < windowLength; index++)
        {
            if (!IsHexDigit(encryptedData[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Decrypt an ASCII-hex encoded eexec segment, skipping over non-hex-digit bytes (typically
    /// whitespace) and discarding the initial4 decrypted seed bytes per the specification.
    /// </summary>
    /// <param name="encryptedHexData">ASCII-hex encoded eexec data.</param>
    /// <returns>Decrypted cleartext span skipping the4-byte seed; may be empty.</returns>
    public static ReadOnlySpan<byte> DecryptEexecAsciiHex(in ReadOnlySpan<byte> encryptedHexData)
    {
        int r = EexecSeed;
        var decoded = new byte[encryptedHexData.Length >> 1];
        int decodedCount = 0;

        for (int index = 0; index < encryptedHexData.Length; index++)
        {
            int highNibble = HexDigitValue(encryptedHexData[index]);
            if (highNibble < 0)
            {
                continue;
            }

            index++;
            int lowNibble = -1;
            while (index < encryptedHexData.Length)
            {
                lowNibble = HexDigitValue(encryptedHexData[index]);
                if (lowNibble >= 0)
                {
                    break;
                }

                index++;
            }

            if (lowNibble < 0)
            {
                break;
            }

            int cipherByte = (highNibble << 4) | lowNibble;
            int plainByte = cipherByte ^ r >> 8;
            decoded[decodedCount++] = (byte)plainByte;
            r = ((cipherByte + r) * C1) + C2 & 0xFFFF;
        }

        if (decodedCount <= 4)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return new ReadOnlySpan<byte>(decoded, 4, decodedCount - 4);
    }

    private static bool IsHexDigit(byte value)
    {
        return (value >= '0' && value <= '9')
            || (value >= 'A' && value <= 'F')
            || (value >= 'a' && value <= 'f');
    }

    private static int HexDigitValue(byte value)
    {
        if (value >= '0' && value <= '9')
        {
            return value - '0';
        }

        if (value >= 'A' && value <= 'F')
        {
            return value - 'A' + 10;
        }

        if (value >= 'a' && value <= 'f')
        {
            return value - 'a' + 10;
        }

        return -1;
    }

    private static bool IsWhitespace(byte value) => value == 0x20 || value == 0x09 || value == 0x0D || value == 0x0A;

    /// <summary>
    /// Decrypt a Type1 CharString program and remove the initial random bytes
    /// determined by the LenIV value (may be zero or &lt;=0 meaning no removal).
    /// </summary>
    /// <param name="encryptedData">Encrypted CharString data.</param>
    /// <param name="lenIV">Random prefix length (LenIV). Values &lt;=0 result in no trimming.</param>
    /// <returns>Decrypted CharString byte array (never null).</returns>
    public static byte[] DecryptCharString(in ReadOnlySpan<byte> encryptedData, int lenIV)
    {
        if (encryptedData.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        int r = CharStringSeed;
        var output = new byte[encryptedData.Length];
        for (int index = 0; index < encryptedData.Length; index++)
        {
            int cipherByte = encryptedData[index];
            int plainByte = cipherByte ^ r >> 8;
            output[index] = (byte)plainByte;
            r = ((cipherByte + r) * C1) + C2 & 0xFFFF;
        }

        if (lenIV > 0 && output.Length > lenIV)
        {
            int trimmedLength = output.Length - lenIV;
            var trimmed = new byte[trimmedLength];
            Buffer.BlockCopy(output, lenIV, trimmed, 0, trimmedLength);
            return trimmed;
        }

        return output;
    }
}
