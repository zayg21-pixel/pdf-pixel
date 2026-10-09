using System;

namespace PdfPixel.Encryption.Cryptography;

/// <summary>
/// Cipher that decrypts data in whole blocks, continuing its state across calls.
/// </summary>
internal interface IDecryptionCipher
{
    /// <summary>
    /// Size of a cipher block in bytes.
    /// </summary>
    int BlockSize { get; }

    /// <summary>
    /// Decrypts whole blocks of <paramref name="source"/> into <paramref name="destination"/>, continuing
    /// the state of previous calls. The spans may be the same.
    /// </summary>
    /// <param name="source">Encrypted data whose length is a multiple of <see cref="BlockSize"/>.</param>
    /// <param name="destination">Destination, at least as long as <paramref name="source"/>.</param>
    void Decrypt(in ReadOnlySpan<byte> source, in Span<byte> destination);

    /// <summary>
    /// Gets the length of the padding at the end of the decrypted <paramref name="lastBlock"/>.
    /// </summary>
    /// <returns>The padding length, or 0 when there is no valid padding.</returns>
    int GetPaddingLength(in ReadOnlySpan<byte> lastBlock);
}
