using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Streams;

/// <summary>
/// Forward-only stream that decodes PDF LZWDecode (ISO 32000-1, 7.4.4).
/// </summary>
public sealed class LzwDecodeStream : Stream
{
    private const int ClearCode = 256;
    private const int EndOfDataCode = 257;
    private const int InitialCodeLength = 9;
    private const int MaxCodeLength = 12;
    private const int MaxDictionarySize = 1 << MaxCodeLength;

    private const int NineBitBoundary = 511;
    private const int TenBitBoundary = 1023;
    private const int ElevenBitBoundary = 2047;

    private readonly Stream _inner;
    private readonly bool _leaveOpen;
    private readonly bool _earlyChange;

    private int _currentCodeLength;
    private int _nextCode;
    private bool _endReached;

    // Bit buffer, consumed MSB-first.
    private int _bitBuffer;
    private int _bitCount;

    private readonly List<byte[]> _dictionary = new(MaxDictionarySize);
    private byte[]? _previousDecoded;

    private readonly List<byte> _outputBytes = [];
    private int _outputIndex;

    #region Stream overrides

    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion

    /// <summary>
    /// Initialize a new LZW decoding stream wrapper.
    /// </summary>
    /// <param name="inner">Compressed LZW stream (must be readable).</param>
    /// <param name="leaveOpen">Leave underlying stream open when disposing.</param>
    /// <param name="earlyChange">Value of the /EarlyChange decode parameter.</param>
    public LzwDecodeStream(Stream inner, bool leaveOpen = false, bool earlyChange = true)
    {
        if (inner == null)
        {
            throw new ArgumentNullException(nameof(inner));
        }

        if (!inner.CanRead)
        {
            throw new ArgumentException("Inner stream must be readable", nameof(inner));
        }

        _inner = inner;
        _leaveOpen = leaveOpen;
        _earlyChange = earlyChange;

        InitializeAllState();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeAllState()
    {
        ResetDictionaryOnly();
        _bitBuffer = 0;
        _bitCount = 0;
        _endReached = false;
        _outputBytes.Clear();
        _outputIndex = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetDictionaryOnly()
    {
        _dictionary.Clear();
        for (int i = 0; i < 256; i++)
        {
            _dictionary.Add(new byte[] { (byte)i });
        }

        // Placeholders for ClearCode and EndOfDataCode.
        _dictionary.Add(Array.Empty<byte>());
        _dictionary.Add(Array.Empty<byte>());

        _currentCodeLength = InitialCodeLength;
        _nextCode = EndOfDataCode + 1;
        _previousDecoded = null;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count == 0)
        {
            return 0;
        }

        int written = 0;
        while (written < count)
        {
            if (_outputIndex >= _outputBytes.Count)
            {
                _outputBytes.Clear();
                _outputIndex = 0;
                if (!DecodeNextCode())
                {
                    break;
                }
            }

            int remaining = _outputBytes.Count - _outputIndex;
            if (remaining <= 0)
            {
                break;
            }

            int toCopy = Math.Min(remaining, count - written);
            for (int i = 0; i < toCopy; i++)
            {
                buffer[offset + written + i] = _outputBytes[_outputIndex + i];
            }

            _outputIndex += toCopy;
            written += toCopy;
        }

        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool DecodeNextCode()
    {
        if (_endReached)
        {
            return false;
        }

        while (true)
        {
            int code = ReadNextCode();
            if (code < 0)
            {
                _endReached = true;
                return false;
            }

            if (code == ClearCode)
            {
                ResetDictionaryOnly();
                continue;
            }

            if (code == EndOfDataCode)
            {
                _endReached = true;
                return false;
            }

            byte[] decoded;
            if (code < _dictionary.Count && _dictionary[code]?.Length > 0)
            {
                decoded = _dictionary[code];
            }
            else if (code == _nextCode && _previousDecoded != null)
            {
                // KwKwK case.
                byte first = _previousDecoded[0];
                decoded = ConcatPrevPlusByte(_previousDecoded, first);
            }
            else
            {
                throw new InvalidDataException($"LZWDecode: malformed code {code} (nextCode={_nextCode}, codeLength={_currentCodeLength}, earlyChange={_earlyChange}).");
            }

            if (_previousDecoded != null)
            {
                byte firstByte = decoded[0];
                if (_nextCode < MaxDictionarySize)
                {
                    _dictionary.Add(ConcatPrevPlusByte(_previousDecoded, firstByte));
                    _nextCode++;

                    int codeOffset = _earlyChange ? 0 : 1;
                    int dictionarySize = _dictionary.Count;

                    if (dictionarySize >= ElevenBitBoundary + codeOffset)
                    {
                        if (_currentCodeLength < MaxCodeLength)
                        {
                            _currentCodeLength = MaxCodeLength;
                        }
                    }
                    else if (dictionarySize >= TenBitBoundary + codeOffset)
                    {
                        if (_currentCodeLength < 11)
                        {
                            _currentCodeLength = 11;
                        }
                    }
                    else if (dictionarySize >= NineBitBoundary + codeOffset)
                    {
                        if (_currentCodeLength < 10)
                        {
                            _currentCodeLength = 10;
                        }
                    }
                }
            }

            _previousDecoded = decoded;

            for (int i = 0; i < decoded.Length; i++)
            {
                _outputBytes.Add(decoded[i]);
            }

            return _outputBytes.Count > 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] ConcatPrevPlusByte(byte[] previous, byte next)
    {
        var combined = new byte[previous.Length + 1];
        Buffer.BlockCopy(previous, 0, combined, 0, previous.Length);
        combined[previous.Length] = next;
        return combined;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadNextCode()
    {
        if (_endReached)
        {
            return -1;
        }

        while (_bitCount < _currentCodeLength)
        {
            int b = _inner.ReadByte();
            if (b < 0)
            {
                return -1;
            }

            _bitBuffer = (_bitBuffer << 8) | b;
            _bitCount += 8;
        }

        int shift = _bitCount - _currentCodeLength;
        int mask = (1 << _currentCodeLength) - 1;
        int code = (_bitBuffer >> shift) & mask;
        _bitCount -= _currentCodeLength;

        if (_bitCount == 0)
        {
            _bitBuffer = 0;
        }
        else
        {
            int remainingMask = (1 << _bitCount) - 1;
            _bitBuffer &= remainingMask;
        }

        return code;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
