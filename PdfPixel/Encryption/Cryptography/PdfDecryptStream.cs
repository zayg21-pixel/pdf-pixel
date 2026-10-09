using System;
using System.IO;

namespace PdfPixel.Encryption.Cryptography;

/// <summary>
/// Forward-only stream that decrypts data read from its source with a block cipher, removing the
/// cipher's padding at the end.
/// </summary>
internal sealed class PdfDecryptStream : Stream
{
    private const int ChunkSize = 2048;

    private readonly Stream _source;
    private readonly IDecryptionCipher _cipher;
    private readonly int _blockSize;
    private readonly byte[] _ciphertext = new byte[ChunkSize];
    private readonly byte[] _plaintext;
    private int _plaintextStart;
    private int _plaintextEnd;
    private bool _sourceEnded;

    /// <summary>
    /// Initializes the stream over <paramref name="source"/>, which it owns.
    /// </summary>
    /// <param name="source">Encrypted data.</param>
    /// <param name="cipher">Cipher that decrypts the data.</param>
    public PdfDecryptStream(Stream source, IDecryptionCipher cipher)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
        _blockSize = cipher.BlockSize;
        _plaintext = new byte[ChunkSize + _blockSize];
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private int ReturnableCount => _plaintextEnd - _plaintextStart - (_sourceEnded ? 0 : _blockSize);

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (offset + count > buffer.Length)
        {
            throw new ArgumentException("offset and count exceed buffer length.", nameof(count));
        }

        if (count == 0)
        {
            return 0;
        }

        while (ReturnableCount <= 0 && !_sourceEnded)
        {
            Fill();
        }

        int returned = Math.Min(count, ReturnableCount);
        if (returned <= 0)
        {
            return 0;
        }

        Buffer.BlockCopy(_plaintext, _plaintextStart, buffer, offset, returned);
        _plaintextStart += returned;

        return returned;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Decrypts the next chunk of whole blocks after the held back last block.
    /// </summary>
    private void Fill()
    {
        int heldCount = _plaintextEnd - _plaintextStart;
        Buffer.BlockCopy(_plaintext, _plaintextStart, _plaintext, 0, heldCount);
        _plaintextStart = 0;
        _plaintextEnd = heldCount;

        int read = ReadFully(_ciphertext, ChunkSize);
        int wholeBlocksLength = read - (read % _blockSize);
        _cipher.Decrypt(_ciphertext.AsSpan(0, wholeBlocksLength), _plaintext.AsSpan(_plaintextEnd));
        _plaintextEnd += wholeBlocksLength;

        if (read < ChunkSize)
        {
            _sourceEnded = true;
            if (_plaintextEnd >= _blockSize)
            {
                _plaintextEnd -= _cipher.GetPaddingLength(_plaintext.AsSpan(_plaintextEnd - _blockSize, _blockSize));
            }
        }
    }

    private int ReadFully(byte[] destination, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = _source.Read(destination, total, count - total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source.Dispose();
        }

        base.Dispose(disposing);
    }
}
