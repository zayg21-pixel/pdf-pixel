using PdfReader.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    partial struct PdfParser
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IPdfValue ReadInlineStream()
        {
            // ID operator already consumed by ReadToken
            // Skip single whitespace after ID per PDF spec
            SkipSingleWhitespaceAfterID();
            
            int dataStart = _parseContext.Position;
            int dataEnd = FindInlineStreamDataEnd(dataStart);
            
            if (dataEnd < 0)
            {
                // Could not find EI terminator - return empty stream
                return PdfValue.InlineStream(PdfString.Empty);
            }
            
            int dataLength = dataEnd - dataStart;
            ReadOnlyMemory<byte> streamData;
            
            if (dataLength > 0)
            {
                streamData = ExtractInlineStreamDataSlice(dataStart, dataLength);
            }
            else
            {
                streamData = default;
            }
            
            _parseContext.Position = dataEnd;
            
            return PdfValue.InlineStream(new PdfString(streamData));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSingleWhitespaceAfterID()
        {
            if (_parseContext.IsAtEnd)
            {
                return;
            }

            byte next = _parseContext.PeekByte();
            if (IsWhitespace(next))
            {
                _parseContext.Advance(1);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyMemory<byte> ExtractInlineStreamDataSlice(int dataStart, int dataLength)
        {
            if (_parseContext.IsSingleMemory)
            {
                return _parseContext.OriginalMemory.Slice(dataStart, dataLength);
            }

            return _parseContext.GetSlice(dataStart, dataLength).ToArray();
        }
        
        /// <summary>
        /// Locate the end position (absolute) of inline image data by finding a valid EI terminator.
        /// Uses the parse context slice for both single-memory and multi-chunk scenarios.
        /// </summary>
        /// <param name="start">Absolute start position of the inline data.</param>
        /// <returns>Absolute end position (start of EI) or -1 if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindInlineStreamDataEnd(int start)
        {
            if (start < 0 || start >= _parseContext.Length)
            {
                return -1;
            }

            ReadOnlySpan<byte> data = _parseContext.GetSlice(start, _parseContext.Length - start);
            int length = data.Length;
            if (length == 0)
            {
                return -1;
            }

            for (int localIndex = 0; localIndex + 1 < length; localIndex++)
            {
                if (data[localIndex] != (byte)'E' || data[localIndex + 1] != (byte)'I')
                {
                    continue;
                }

                bool precedingWhitespace = localIndex == 0 || IsWhitespace(data[localIndex - 1]);
                byte following = localIndex + 2 < length ? data[localIndex + 2] : (byte)0;
                bool followingDelimiter = localIndex + 2 >= length || IsTokenTerminator(following);

                if (precedingWhitespace && followingDelimiter)
                {
                    // Return absolute position within original context
                    return start + localIndex;
                }
            }

            return -1;
        }
    }
}