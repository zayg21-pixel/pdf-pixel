using System;

namespace PdfPixel.Encryption.Cryptography;

/// <summary>
/// Pure managed RC4 stream cipher.
/// </summary>
internal sealed class Rc4 : IDecryptionCipher
{
    private readonly byte[] _state = new byte[256];
    private int _index;
    private int _swapIndex;

    /// <summary>
    /// Initializes the cipher state from <paramref name="key"/>.
    /// </summary>
    public Rc4(in ReadOnlySpan<byte> key)
    {
        for (int i = 0; i < 256; i++)
        {
            _state[i] = (byte)i;
        }

        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + _state[i] + key[i % key.Length]) & 0xFF;
            (_state[i], _state[j]) = (_state[j], _state[i]);
        }
    }

    /// <inheritdoc />
    public int BlockSize => 1;

    /// <inheritdoc />
    public void Decrypt(in ReadOnlySpan<byte> source, in Span<byte> destination)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination is shorter than source.", nameof(destination));
        }

        byte[] state = _state;
        int index = _index;
        int swapIndex = _swapIndex;
        for (int position = 0; position < source.Length; position++)
        {
            index = (index + 1) & 0xFF;
            swapIndex = (swapIndex + state[index]) & 0xFF;
            (state[index], state[swapIndex]) = (state[swapIndex], state[index]);
            destination[position] = (byte)(source[position] ^ state[(state[index] + state[swapIndex]) & 0xFF]);
        }

        _index = index;
        _swapIndex = swapIndex;
    }

    /// <inheritdoc />
    public int GetPaddingLength(in ReadOnlySpan<byte> lastBlock) => 0;
}
