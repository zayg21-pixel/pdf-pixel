using System;
using System.Collections.Generic;
using System.IO;

namespace PdfRender.Streams
{
    /// <summary>
    /// Forward-only streaming decoder for the PDF LZWDecode filter (ISO32000-1,7.4.4).
    /// Supports /EarlyChange parameter (default1) and emits decoded bytes incrementally.
    /// Responsibilities limited to pure LZW expansion; predictor handling is done externally.
    /// </summary>
    internal sealed class LzwDecodeStream : Stream
    {
        // Control codes & limits
        private const int ClearCode = 256; // Reset dictionary sentinel
        private const int EndOfDataCode = 257; // Termination sentinel
        private const int InitialCodeLength = 9; // Start at9 bits
        private const int MaxCodeLength = 12; // PDF limit per spec
        private const int MaxDictionarySize = 1 << MaxCodeLength; //4096 entries

        // Fixed boundaries matching working LzwFilter
        private const int NineBitBoundary = 511;   // 2^9 - 1
        private const int TenBitBoundary = 1023;   // 2^10 - 1
        private const int ElevenBitBoundary = 2047; // 2^11 - 1

        // Underlying compressed stream
        private readonly Stream _inner;
        private readonly bool _leaveOpen;
        private readonly bool _earlyChange; // true => grow when last allocated code == (2^n -1); false => grow when last allocated code ==2^n

        // Dynamic state
        private int _currentCodeLength;
        private int _nextCode; // Next free dictionary index
        private bool _endReached; // End-of-data reached

        // Bit buffer (MSB-first consumption)
        private int _bitBuffer;
        private int _bitCount;

        // Dictionary entries (index => byte sequence)
        private readonly List<byte[]> _dictionary = new List<byte[]>(MaxDictionarySize);
        private byte[] _previousDecoded; // Last decoded sequence (for building new entries)

        // Output staging of current decoded sequence to satisfy caller Read requests.
        private readonly List<byte> _outputBytes = new List<byte>();
        private int _outputIndex;

        #region Stream overrides
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
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
        /// <param name="earlyChange">/EarlyChange parameter (default true =>1).</param>
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

        /// <summary>
        /// Performs a full initialization of decoder state (used at construction only).
        /// </summary>
        private void InitializeAllState()
        {
            ResetDictionaryOnly();
            _bitBuffer = 0;
            _bitCount = 0;
            _endReached = false;
            _outputBytes.Clear();
            _outputIndex = 0;
        }

        /// <summary>
        /// Resets only the dictionary-related state required when encountering a ClearCode.
        /// IMPORTANT: Does not reset bit buffer so that partially read bytes remain valid for subsequent codes.
        /// </summary>
        private void ResetDictionaryOnly()
        {
            _dictionary.Clear();
            for (int i = 0; i < 256; i++)
            {
                _dictionary.Add(new byte[] { (byte)i });
            }
            // Placeholders for control codes (never output directly)
            _dictionary.Add(Array.Empty<byte>()); //256 Clear
            _dictionary.Add(Array.Empty<byte>()); //257 EndOfData

            _currentCodeLength = InitialCodeLength;
            _nextCode = EndOfDataCode + 1; // First free code index
            _previousDecoded = null;
        }

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
                        break; // No more decoded data
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
                    // Reset dictionary without discarding any buffered bits
                    ResetDictionaryOnly();
                    continue; // Need next code after clear
                }
                if (code == EndOfDataCode)
                {
                    _endReached = true;
                    return false;
                }

                byte[] decoded;
                if (code < _dictionary.Count && _dictionary[code] != null && _dictionary[code].Length > 0)
                {
                    decoded = _dictionary[code];
                }
                else if (code == _nextCode && _previousDecoded != null)
                {
                    // KwKwK case: code references entry being formed (prev + first byte of prev)
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

                        if (dictionarySize >= ElevenBitBoundary + codeOffset) // 2047 + offset
                        {
                            if (_currentCodeLength < MaxCodeLength)
                            {
                                _currentCodeLength = MaxCodeLength; // 12 bits
                            }
                        }
                        else if (dictionarySize >= TenBitBoundary + codeOffset) // 1023 + offset
                        {
                            if (_currentCodeLength < 11)
                            {
                                _currentCodeLength = 11;
                            }
                        }
                        else if (dictionarySize >= NineBitBoundary + codeOffset) // 511 + offset
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

        private static byte[] ConcatPrevPlusByte(byte[] previous, byte next)
        {
            byte[] combined = new byte[previous.Length + 1];
            Buffer.BlockCopy(previous, 0, combined, 0, previous.Length);
            combined[previous.Length] = next;
            return combined;
        }

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

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}