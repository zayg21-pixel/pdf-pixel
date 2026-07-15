using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    private static readonly PdfString FilterAbbreviationKey = (PdfString)"F"u8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue? ReadInlineStream(Stack<IPdfValue>? operandStack)
    {
        // Skip single whitespace after ID per PDF spec.
        int beginPosition = Position;
        byte firstByte = PeekByte();
        if (firstByte == CarriageReturn)
        {
            Advance(1);
            if (!IsAtEnd && PeekByte() == LineFeed)
            {
                Advance(1);
            }
        }
        else if (IsWhitespace(firstByte))
        {
            Advance(1);
        }

        _localBuffer.Clear();

        PdfFilterType? filter = ResolveOuterFilter(operandStack);
        bool endFound = filter switch
        {
            PdfFilterType.ASCII85Decode => TryReadUntilAscii85Eod(),
            _ => TryScanEi()
        };

        if (!endFound)
        {
            Position = beginPosition;
            return null;
        }

        if (_localBuffer.Count == 0)
        {
            return PdfValueFactory.InlineStream(PdfString.Empty);
        }

        return PdfValueFactory.InlineStream(new PdfString([.. _localBuffer]));
    }

    // First entry of /F or /Filter, which is the one applied to the raw stream bytes.
    private static PdfFilterType? ResolveOuterFilter(Stack<IPdfValue>? operandStack)
    {
        if (operandStack == null || operandStack.Count == 0)
        {
            return null;
        }

        List<IPdfValue> parameters = new(operandStack);
        parameters.Reverse();

        for (int parameterIndex = 0; parameterIndex + 1 < parameters.Count; parameterIndex += 2)
        {
            IPdfValue keyValue = parameters[parameterIndex];
            if (keyValue.Type != PdfValueType.Name)
            {
                continue;
            }

            PdfString key = keyValue.AsName();
            if (key != PdfTokens.FilterKey && key != FilterAbbreviationKey)
            {
                continue;
            }

            List<PdfFilterType> filters = PdfStreamDecoder.GetFilters(parameters[parameterIndex + 1]);
            return (filters.Count > 0) ? filters[0] : null;
        }

        return null;
    }

    private bool TryReadUntilAscii85Eod()
    {
        while (!IsAtEnd)
        {
            byte current = ReadByte();
            _localBuffer.Add(current);

            if (current == (byte)'~' && !IsAtEnd && PeekByte() == (byte)'>')
            {
                Advance(1);
                _localBuffer.Add((byte)'>');
                return SkipToEiOperator();
            }
        }

        return false;
    }

    private bool SkipToEiOperator()
    {
        while (!IsAtEnd && IsWhitespace(PeekByte()))
        {
            Advance(1);
        }

        if (IsAtEnd || PeekByte() != (byte)'E' || Position + 1 >= Length || PeekByte(1) != (byte)'I')
        {
            return false;
        }

        byte following = (Position + 2 < Length) ? PeekByte(2) : (byte)0;
        return (Position + 2 >= Length) || IsTokenTerminator(following);
    }

    private bool TryScanEi()
    {
        int previousByte = -1;

        while (!IsAtEnd)
        {
            byte current = ReadByte();

            if (current == (byte)'E' && !IsAtEnd)
            {
                byte next = PeekByte();
                if (next == (byte)'I')
                {
                    bool precedingWhitespace = previousByte == -1 || IsWhitespace((byte)previousByte);
                    byte following = (Position + 1 < Length) ? PeekByte(1) : (byte)0;
                    bool followingDelimiter = (Position + 1 >= Length) || IsTokenTerminator(following);

                    if (precedingWhitespace && followingDelimiter)
                    {
                        SetPosition(Position - 1);
                        return true;
                    }
                }
            }

            _localBuffer.Add(current);
            previousByte = current;
        }

        return false;
    }
}
