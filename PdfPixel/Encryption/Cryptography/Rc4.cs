using System;

namespace PdfPixel.Encryption.Cryptography;

/// <summary>
/// Pure managed RC4 stream cipher.
/// </summary>
internal static class Rc4
{
    /// <summary>
    /// Encrypts or decrypts <paramref name="data"/> with <paramref name="key"/>.
    /// </summary>
    public static byte[] Transform(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> data)
    {
        Span<byte> state = stackalloc byte[256];
        for (int i = 0; i < 256; i++)
        {
            state[i] = (byte)i;
        }

        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
        }

        var output = new byte[data.Length];
        int index = 0;
        j = 0;
        for (int position = 0; position < data.Length; position++)
        {
            index = (index + 1) & 0xFF;
            j = (j + state[index]) & 0xFF;
            (state[index], state[j]) = (state[j], state[index]);
            output[position] = (byte)(data[position] ^ state[(state[index] + state[j]) & 0xFF]);
        }

        return output;
    }
}
