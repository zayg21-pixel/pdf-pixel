using PdfPixel.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

partial struct PdfParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue ReadInlineStream()
    {
        // ID operator already consumed by ReadToken
        // Skip single whitespace after ID per PDF spec
        if (IsWhitespace(PeekByte()))
        {
            Advance(1);
        }

        _localBuffer.Clear();

        int previousByte = -1; // Tracks last consumed byte for whitespace check before potential EI.
        while (!IsAtEnd)
        {
            byte current = ReadByte();

            if (current == (byte)'E' && !IsAtEnd)
            {
                byte next = PeekByte();
                if (next == (byte)'I')
                {
                    bool precedingWhitespace = previousByte == -1 || IsWhitespace((byte)previousByte);
                    byte following = (Position + 1 < Length) ? PeekByte(1) : (byte)0; // after 'E'+'I'
                    bool followingDelimiter = (Position + 1 >= Length) || IsTokenTerminator(following);

                    if (precedingWhitespace && followingDelimiter)
                    {
                        // Roll back the consumed 'E' since EI marks end; leave Position at start of 'E'.
                        SetPosition(Position - 1);
                        break;
                    }
                }
            }

            _localBuffer.Add(current);
            previousByte = current;
        }

        if (_localBuffer.Count == 0)
        {
            return PdfValueFactory.InlineStream(PdfString.Empty);
        }

        return PdfValueFactory.InlineStream(new PdfString([.. _localBuffer]));
    }
}