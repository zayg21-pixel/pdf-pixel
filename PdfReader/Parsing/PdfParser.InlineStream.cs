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
            
            int dataStart = Position;
            int dataEnd = FindInlineStreamDataEnd(dataStart);
            Position = dataStart;

            if (dataEnd < 0)
            {
                // Could not find EI terminator - return empty stream
                return PdfValue.InlineStream(PdfString.Empty);
            }
            
            int dataLength = dataEnd - dataStart;
            ReadOnlyMemory<byte> streamData;
            
            if (dataLength > 0)
            {
                streamData = ReadSliceFromCurrent(dataLength).ToArray();
            }
            else
            {
                streamData = default;
            }
            
            return PdfValue.InlineStream(new PdfString(streamData));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSingleWhitespaceAfterID()
        {
            if (IsAtEnd)
            {
                return;
            }

            byte next = PeekByte();
            if (IsWhitespace(next))
            {
                Advance(1);
            }
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
            if (start <0 || start >= Length)
            {
                return -1;
            }

            int position = start;
            int previousByte = -1;
            SetPosition(position);

            while (!IsAtEnd)
            {
                byte currentByte = ReadByte();
                position++;

                if (currentByte == (byte)'E' && !IsAtEnd)
                {
                    byte nextByte = ReadByte();
                    position++;
                    if (nextByte == (byte)'I')
                    {
                        bool precedingWhitespace = previousByte == -1 || IsWhitespace((byte)previousByte);
                        byte following = !IsAtEnd ? PeekByte() : (byte)0;
                        bool followingDelimiter = IsAtEnd || IsTokenTerminator(following);

                        if (precedingWhitespace && followingDelimiter)
                        {
                            // Return absolute position of 'E' (start of EI)
                            return position -2;
                        }
                    }
                    previousByte = currentByte;
                    continue;
                }
                previousByte = currentByte;
            }

            return -1;
        }
    }
}