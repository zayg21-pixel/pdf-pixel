using System;
using System.Collections.Generic;

namespace PdfReader.Parsing
{
    /// <summary>
    /// High-performance sequential access to multiple memory chunks as if they were one contiguous memory blob.
    /// Optimized to eliminate expensive loops by maintaining current chunk state and cached span access.
    /// </summary>
    public ref struct SequentialMemoryReader
    {
        private readonly List<ReadOnlyMemory<byte>> _chunks;
        private readonly int[] _chunkStartPositions; // Precomputed cumulative positions for fast lookup
        private int _currentChunkIndex;
        private int _positionInCurrentChunk;
        private int _totalPosition;
        private readonly int _totalLength;
        private ReadOnlySpan<byte> _currentChunkSpan; // Cached span for current chunk - key optimization!

        public SequentialMemoryReader(List<ReadOnlyMemory<byte>> chunks)
        {
            _chunks = chunks;
            _currentChunkIndex = 0;
            _positionInCurrentChunk = 0;
            _totalPosition = 0;
            
            // Precompute chunk start positions for O(log n) binary search instead of O(n) linear search
            _chunkStartPositions = new int[chunks.Count + 1];
            int total = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                _chunkStartPositions[i] = total;
                total += chunks[i].Length;
            }
            _chunkStartPositions[chunks.Count] = total;
            _totalLength = total;

            // Cache current chunk span for fast access - avoids repeated .Span calls
            _currentChunkSpan = chunks.Count > 0 ? chunks[0].Span : ReadOnlySpan<byte>.Empty;
        }

        /// <summary>
        /// Current position across all chunks
        /// </summary>
        public int Position => _totalPosition;

        /// <summary>
        /// Total length across all chunks
        /// </summary>
        public int Length => _totalLength;

        /// <summary>
        /// Check if we have reached the end of all chunks
        /// </summary>
        public bool IsAtEnd => _totalPosition >= _totalLength;

        /// <summary>
        /// Peek at a byte at the specified offset from current position without advancing
        /// Optimized to use cached span for current chunk access
        /// </summary>
        public byte PeekByte(int offset = 0)
        {
            int targetPosition = _totalPosition + offset;
            if (targetPosition >= _totalLength || targetPosition < 0)
                return 0;

            // Fast path: target is in current chunk - use cached span (O(1))
            if (_currentChunkIndex < _chunks.Count)
            {
                int targetPosInCurrentChunk = _positionInCurrentChunk + offset;
                if (targetPosInCurrentChunk >= 0 && targetPosInCurrentChunk < _currentChunkSpan.Length)
                {
                    return _currentChunkSpan[targetPosInCurrentChunk]; // Direct cached access!
                }
            }

            // Slow path: target is in a different chunk - use binary search (O(log n))
            return PeekByteAt(targetPosition);
        }

        /// <summary>
        /// Read a byte and advance position
        /// Optimized for sequential access patterns using cached span
        /// </summary>
        public byte ReadByte()
        {
            if (IsAtEnd)
                return 0;

            // Fast path: read from cached current chunk span (O(1))
            if (_currentChunkIndex < _chunks.Count && _positionInCurrentChunk < _currentChunkSpan.Length)
            {
                byte result = _currentChunkSpan[_positionInCurrentChunk]; // Direct cached access!
                _positionInCurrentChunk++;
                _totalPosition++;

                // Check if we need to move to next chunk
                if (_positionInCurrentChunk >= _currentChunkSpan.Length)
                {
                    MoveToNextChunk();
                }

                return result;
            }

            // Should not reach here in normal operation
            return 0;
        }

        /// <summary>
        /// Advance position by the specified number of bytes efficiently
        /// Optimized to handle bulk advances without calling ReadByte() in a loop
        /// </summary>
        public void Advance(int count)
        {
            if (count <= 0 || IsAtEnd)
                return;

            int targetPosition = _totalPosition + count;
            if (targetPosition > _totalLength)
                targetPosition = _totalLength;

            // Fast path: advance within current chunk using cached span (O(1))
            if (_currentChunkIndex < _chunks.Count)
            {
                int newPosInChunk = _positionInCurrentChunk + count;
                if (newPosInChunk <= _currentChunkSpan.Length) // Can advance within current chunk
                {
                    _positionInCurrentChunk = newPosInChunk;
                    _totalPosition = targetPosition;
                    
                    // Check if we reached the end of current chunk
                    if (_positionInCurrentChunk >= _currentChunkSpan.Length)
                    {
                        MoveToNextChunk();
                    }
                    return;
                }
            }

            // Slow path: advance across chunks (O(log n))
            SetPosition(targetPosition);
        }

        /// <summary>
        /// Get a slice of bytes from current position as ReadOnlySpan
        /// Optimized to use cached span when possible to avoid copying
        /// </summary>
        public ReadOnlySpan<byte> GetSlice(int length)
        {
            if (length <= 0 || IsAtEnd)
                return ReadOnlySpan<byte>.Empty;

            // Clamp length to available data
            int available = _totalLength - _totalPosition;
            if (length > available)
                length = available;

            // Fast path: slice fits entirely in current chunk - use cached span (O(1))
            if (_currentChunkIndex < _chunks.Count)
            {
                int remainingInChunk = _currentChunkSpan.Length - _positionInCurrentChunk;
                if (length <= remainingInChunk)
                {
                    return _currentChunkSpan.Slice(_positionInCurrentChunk, length); // Direct cached access!
                }
            }

            // Slow path: slice spans multiple chunks - need to copy
            return GetSliceAcrossChunks(length);
        }

        /// <summary>
        /// Get a slice from the specified start position and length without changing current position
        /// Optimized to avoid unnecessary position changes when possible
        /// </summary>
        public ReadOnlySpan<byte> GetSliceAt(int start, int length)
        {
            if (length <= 0 || start >= _totalLength || start < 0)
                return ReadOnlySpan<byte>.Empty;

            // Clamp length to available data
            int available = _totalLength - start;
            if (length > available)
                length = available;

            // Fast path: slice is entirely in current chunk (O(1))
            if (_currentChunkIndex < _chunks.Count)
            {
                int chunkStart = _chunkStartPositions[_currentChunkIndex];
                int chunkEnd = _chunkStartPositions[_currentChunkIndex + 1];
                
                if (start >= chunkStart && start + length <= chunkEnd)
                {
                    // Slice is entirely in current chunk - use cached span directly
                    int offsetInChunk = start - chunkStart;
                    return _currentChunkSpan.Slice(offsetInChunk, length);
                }
            }

            // Slow path: slice is in different chunk(s) - need to set position temporarily
            int originalPosition = _totalPosition;
            int originalChunkIndex = _currentChunkIndex;
            int originalPositionInChunk = _positionInCurrentChunk;
            var originalChunkSpan = _currentChunkSpan;

            SetPosition(start);
            var result = GetSlice(length);

            // Restore original position and cached state
            _totalPosition = originalPosition;
            _currentChunkIndex = originalChunkIndex;
            _positionInCurrentChunk = originalPositionInChunk;
            _currentChunkSpan = originalChunkSpan;

            return result;
        }

        /// <summary>
        /// Get slice that spans multiple chunks - requires copying
        /// </summary>
        private ReadOnlySpan<byte> GetSliceAcrossChunks(int length)
        {
            var buffer = new byte[length];
            int copied = 0;
            int tempChunkIndex = _currentChunkIndex;
            int tempPositionInChunk = _positionInCurrentChunk;

            while (copied < length && tempChunkIndex < _chunks.Count)
            {
                var chunk = _chunks[tempChunkIndex];
                int availableInChunk = chunk.Length - tempPositionInChunk;
                int toCopy = Math.Min(length - copied, availableInChunk);

                chunk.Span.Slice(tempPositionInChunk, toCopy).CopyTo(buffer.AsSpan(copied));
                copied += toCopy;

                tempChunkIndex++;
                tempPositionInChunk = 0;
            }

            return buffer.AsSpan(0, copied);
        }

        /// <summary>
        /// Set position to a specific location (for backtracking)
        /// Optimized to avoid unnecessary work when staying in the same chunk
        /// </summary>
        public void SetPosition(int position)
        {
            if (position < 0)
                position = 0;
            if (position > _totalLength)
                position = _totalLength;

            _totalPosition = position;

            if (position == _totalLength)
            {
                // Position at end
                _currentChunkIndex = _chunks.Count;
                _positionInCurrentChunk = 0;
                _currentChunkSpan = ReadOnlySpan<byte>.Empty;
                return;
            }

            // Fast path: Check if we're staying in the same chunk (O(1))
            if (_currentChunkIndex < _chunks.Count)
            {
                int chunkStart = _chunkStartPositions[_currentChunkIndex];
                int chunkEnd = _chunkStartPositions[_currentChunkIndex + 1];
                
                if (position >= chunkStart && position < chunkEnd)
                {
                    // We're staying in the same chunk - just update position (O(1))
                    _positionInCurrentChunk = position - chunkStart;
                    // No need to update _currentChunkSpan - it's already correct!
                    return;
                }
            }

            // Slow path: We're moving to a different chunk - binary search and update span (O(log n))
            int chunkIndex = BinarySearchChunk(position);
            _currentChunkIndex = chunkIndex;
            _positionInCurrentChunk = position - _chunkStartPositions[chunkIndex];

            // Update cached chunk span only when changing chunks
            if (chunkIndex < _chunks.Count)
            {
                _currentChunkSpan = _chunks[chunkIndex].Span;
            }
            else
            {
                _currentChunkSpan = ReadOnlySpan<byte>.Empty;
            }
        }

        /// <summary>
        /// Check if a sequence matches at the specified position without advancing
        /// </summary>
        public bool MatchSequenceAt(int position, ReadOnlySpan<byte> sequence)
        {
            if (position + sequence.Length > _totalLength)
                return false;

            for (int i = 0; i < sequence.Length; i++)
            {
                if (PeekByteAt(position + i) != sequence[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Move to the next chunk and update cached span - key optimization method
        /// </summary>
        private void MoveToNextChunk()
        {
            _currentChunkIndex++;
            _positionInCurrentChunk = 0;

            // Update cached span for new current chunk
            if (_currentChunkIndex < _chunks.Count)
            {
                _currentChunkSpan = _chunks[_currentChunkIndex].Span; // Cache new chunk span
            }
            else
            {
                _currentChunkSpan = ReadOnlySpan<byte>.Empty;
            }
        }

        /// <summary>
        /// Binary search to find which chunk contains the given position
        /// Returns chunk index (O(log n) instead of O(n))
        /// </summary>
        private int BinarySearchChunk(int position)
        {
            int left = 0;
            int right = _chunks.Count - 1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                int chunkStart = _chunkStartPositions[mid];
                int chunkEnd = _chunkStartPositions[mid + 1];

                if (position >= chunkStart && position < chunkEnd)
                {
                    return mid;
                }
                else if (position < chunkStart)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            // Should not reach here for valid positions
            return _chunks.Count;
        }

        /// <summary>
        /// Peek at a byte at a specific absolute position using binary search
        /// Optimized with O(log n) complexity instead of O(n)
        /// </summary>
        private byte PeekByteAt(int position)
        {
            if (position >= _totalLength || position < 0)
                return 0;

            int chunkIndex = BinarySearchChunk(position);
            if (chunkIndex >= _chunks.Count)
                return 0;

            int positionInChunk = position - _chunkStartPositions[chunkIndex];
            return _chunks[chunkIndex].Span[positionInChunk]; // Only access .Span when necessary
        }
    }
}