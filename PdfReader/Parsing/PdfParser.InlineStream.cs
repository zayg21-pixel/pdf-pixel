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

            var span = _parseContext.GetSlice(dataStart, dataLength);
            return span.ToArray();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindInlineStreamDataEnd(int start)
        {
            if (!_parseContext.IsSingleMemory)
            {
                return -1; // Multi-chunk fallback not implemented
            }

            var span = _parseContext.OriginalMemory.Span;
            int length = span.Length;
            
            for (int index = start; index + 1 < length; index++)
            {
                if (span[index] == (byte)'E' && span[index + 1] == (byte)'I')
                {
                    // Check for proper EI delimiter according to PDF spec:
                    // - Must be preceded by whitespace (or be at start)
                    // - Must be followed by whitespace or delimiter (or be at end)
                    bool precedingWhitespace = index - 1 >= start && IsWhitespace(span[index - 1]);
                    byte following = index + 2 < length ? span[index + 2] : (byte)0;
                    bool followingDelimiter = index + 2 >= length || IsTokenTerminator(following);
                    
                    if (precedingWhitespace && followingDelimiter)
                    {
                        return index;
                    }
                }
            }
            
            return -1;
        }
    }
}