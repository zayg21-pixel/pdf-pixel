using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    /// <summary>
    /// High-performance PDF parse context optimized for both single and multiple memory chunks.
    /// Uses direct memory/span access for single chunks (fastest path) and SequentialMemoryReader for multi-chunk scenarios.
    /// </summary>
    public ref struct PdfParseContext
    {
        private readonly ReadOnlyMemory<byte> _singleMemory;
        private readonly ReadOnlySpan<byte> _singleSpan;
        private int _position;
        private readonly int _length;
        private readonly bool _isSingleMemory;

        private SequentialMemoryReader _reader;

        // Single memory constructors (optimized fast path)
        public PdfParseContext(ReadOnlyMemory<byte> memory)
        {
            _singleMemory = memory;
            _singleSpan = memory.Span;
            _position = 0;
            _length = memory.Length;
            _isSingleMemory = true;
            _reader = default;
        }

        // Multi-memory constructor (fallback to SequentialMemoryReader)
        public PdfParseContext(List<ReadOnlyMemory<byte>> chunks)
        {
            if (chunks.Count <= 1)
            {
                // Optimize single chunk case
                if (chunks.Count == 0)
                {
                    _singleMemory = default;
                    _singleSpan = ReadOnlySpan<byte>.Empty;
                    _position = 0;
                    _length = 0;
                }
                else
                {
                    _singleMemory = chunks[0];
                    _singleSpan = chunks[0].Span;
                    _position = 0;
                    _length = chunks[0].Length;
                }
                _isSingleMemory = true;
                _reader = default;
            }
            else
            {
                // Multi-chunk case - use SequentialMemoryReader
                _singleMemory = default;
                _singleSpan = default;
                _position = 0;
                _length = 0;
                _isSingleMemory = false;
                _reader = new SequentialMemoryReader(chunks);
            }
        }

        /// <summary>
        /// Current position in the stream (optimized for single memory)
        /// </summary>
        public int Position 
        { 
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isSingleMemory ? _position : _reader.Position;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set 
            {
                if (_isSingleMemory)
                {
                    _position = Math.Max(0, Math.Min(value, _length));
                }
                else
                {
                    _reader.SetPosition(value);
                }
            }
        }

        /// <summary>
        /// Total length of all memory chunks combined
        /// </summary>
        public int Length => _isSingleMemory ? _length : _reader.Length;

        /// <summary>
        /// Check if we're at the end of all memory chunks
        /// </summary>
        public bool IsAtEnd => _isSingleMemory ? _position >= _length : _reader.IsAtEnd;

        /// <summary>
        /// Indicates whether this context contains a single memory chunk (for optimization)
        /// </summary>
        public bool IsSingleMemory => _isSingleMemory;

        /// <summary>
        /// Gets the original memory segment represented as a read-only sequence of bytes.
        /// </summary>
        public ReadOnlyMemory<byte> OriginalMemory => _isSingleMemory ? _singleMemory : default;

        /// <summary>
        /// Peek at a byte without advancing position (optimized for single memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte PeekByte(int offset = 0)
        {
            if (_isSingleMemory)
            {
                int targetPosition = _position + offset;
                if (targetPosition >= 0 && targetPosition < _length)
                {
                    return _singleSpan[targetPosition]; // Direct span access - fastest!
                }
                return 0;
            }
            else
            {
                return _reader.PeekByte(offset);
            }
        }

        /// <summary>
        /// Read a byte and advance position (optimized for single memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if (_isSingleMemory)
            {
                if (_position < _length)
                {
                    return _singleSpan[_position++]; // Direct span access - fastest!
                }
                return 0;
            }
            else
            {
                return _reader.ReadByte();
            }
        }

        /// <summary>
        /// Get a slice from the specified position and length (optimized for single memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetSlice(int start, int length)
        {
            if (_isSingleMemory)
            {
                if (start < 0 || start >= _length)
                    return ReadOnlySpan<byte>.Empty;
                
                int available = _length - start;
                if (length > available)
                    length = available;
                
                if (length <= 0)
                    return ReadOnlySpan<byte>.Empty;
                
                return _singleSpan.Slice(start, length); // Direct span slice - fastest!
            }
            else
            {
                return _reader.GetSliceAt(start, length);
            }
        }

        /// <summary>
        /// Check if a sequence matches at the specified position (optimized for single memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchSequenceAt(int position, ReadOnlySpan<byte> sequence)
        {
            if (_isSingleMemory)
            {
                if (position + sequence.Length > _length || position < 0)
                    return false;
                
                var slice = _singleSpan.Slice(position, sequence.Length);
                return slice.SequenceEqual(sequence); // Direct span comparison - fastest!
            }
            else
            {
                return _reader.MatchSequenceAt(position, sequence);
            }
        }

        /// <summary>
        /// Advance position by the specified number of bytes (optimized for single memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (_isSingleMemory)
            {
                _position = Math.Min(_position + count, _length);
            }
            else
            {
                _reader.Advance(count);
            }
        }

        /// <summary>
        /// Get slice from current position (optimized for single memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetSliceFromCurrent(int length)
        {
            if (_isSingleMemory)
            {
                if (_position >= _length)
                    return ReadOnlySpan<byte>.Empty;
                
                int available = _length - _position;
                if (length > available)
                    length = available;
                
                if (length <= 0)
                    return ReadOnlySpan<byte>.Empty;
                
                return _singleSpan.Slice(_position, length); // Direct span slice - fastest!
            }
            else
            {
                return _reader.GetSlice(length);
            }
        }
    }
}