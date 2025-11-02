using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    partial struct PdfParser
    {
        private const byte ForwardSlash = (byte)'/';
        private const byte LeftParen = (byte)'(';
        private const byte RightParen = (byte)')';
        private const byte LeftAngle = (byte)'<';
        private const byte RightAngle = (byte)'>';
        private const byte LeftSquare = (byte)'[';
        private const byte RightSquare = (byte)']';
        private const byte Plus = (byte)'+';
        private const byte Minus = (byte)'-';
        private const byte Reference = (byte)'R';
        private const byte Dot = (byte)'.';
        private const byte Backslash = (byte)'\\';
        private const byte Zero = (byte)'0';
        private const byte Nine = (byte)'9';
        public const byte Separator = (byte)' ';
        public const byte CarriageReturn = (byte)'\r';
        public const byte LineFeed = (byte)'\n';
        private const byte CommentStart = (byte)'%';
        // Added missing whitespace control characters for skip logic.
        private const byte Tab = (byte)'\t';
        private const byte FormFeed = (byte)'\f';
        private const byte NullChar = (byte)'\0';

        private enum PdfTokenType
        {
            ArrayStart,
            ArrayEnd,
            DictionaryStart,
            DictionaryEnd,
            Number,
            Operator,
            Name,
            HexString,
            HexStringEnd,
            String,
            StringEnd,
            Reference,
            InlineStreamStart,
            ContextEnd // Signals end of current parsing context (buffer end or explicit termination)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PdfTokenType ReadToken()
        {
            SkipWhitespacesAndComments();

            if (_parseContext.IsAtEnd)
            {
                return PdfTokenType.ContextEnd;
            }

            byte current = _parseContext.PeekByte();

            // Boundary checks for lookahead removed: PeekByte(n) returns 0 when out-of-range.
            switch (current)
            {
                case LeftSquare:
                {
                    _parseContext.Advance(1);
                    return PdfTokenType.ArrayStart;
                }
                case RightSquare:
                {
                    _parseContext.Advance(1);
                    return PdfTokenType.ArrayEnd;
                }
                case LeftAngle:
                {
                    if (_parseContext.PeekByte(1) == LeftAngle)
                    {
                        _parseContext.Advance(2);
                        return PdfTokenType.DictionaryStart;
                    }
                    _parseContext.Advance(1);
                    return PdfTokenType.HexString;
                }
                case RightAngle:
                {
                    if (_parseContext.PeekByte(1) == RightAngle)
                    {
                        _parseContext.Advance(2);
                        return PdfTokenType.DictionaryEnd;
                    }
                    _parseContext.Advance(1);
                    return PdfTokenType.HexStringEnd;
                }
                case ForwardSlash:
                {
                    _parseContext.Advance(1);
                    return PdfTokenType.Name;
                }
                case LeftParen:
                {
                    _parseContext.Advance(1);
                    return PdfTokenType.String;
                }
                case RightParen:
                {
                    _parseContext.Advance(1);
                    return PdfTokenType.StringEnd;
                }
                case Plus:
                case Minus:
                case Dot:
                {
                    return PdfTokenType.Number;
                }
                default:
                {
                    if (_allowReferences && current == Reference && IsTokenTerminator(_parseContext.PeekByte(1)))
                    {
                        _parseContext.Advance(1);
                        return PdfTokenType.Reference;
                    }

                    if (IsDigit(current))
                    {
                        return PdfTokenType.Number;
                    }

                    // Check for ID operator (inline stream start)
                    if (current == (byte)'I' && _parseContext.PeekByte(1) == (byte)'D')
                    {
                        // Verify it's followed by whitespace or delimiter
                        if (IsTokenTerminator(_parseContext.PeekByte(2)))
                        {
                            _parseContext.Advance(2); // Consume "ID"
                            return PdfTokenType.InlineStreamStart;
                        }
                    }

                    return PdfTokenType.Operator;
                }
            }
        }
    }
}
