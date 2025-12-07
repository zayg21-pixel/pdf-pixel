using System.Runtime.CompilerServices;

namespace PdfReader.Parsing;

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

        if (IsAtEnd)
        {
            return PdfTokenType.ContextEnd;
        }

        byte current = PeekByte();

        // Boundary checks for lookahead removed: PeekByte(n) returns 0 when out-of-range.
        switch (current)
        {
            case LeftSquare:
            {
                Advance(1);
                return PdfTokenType.ArrayStart;
            }
            case RightSquare:
            {
                Advance(1);
                return PdfTokenType.ArrayEnd;
            }
            case LeftAngle:
            {
                if (PeekByte(1) == LeftAngle)
                {
                    Advance(2);
                    return PdfTokenType.DictionaryStart;
                }
                Advance(1);
                return PdfTokenType.HexString;
            }
            case RightAngle:
            {
                if (PeekByte(1) == RightAngle)
                {
                    Advance(2);
                    return PdfTokenType.DictionaryEnd;
                }
                Advance(1);
                return PdfTokenType.HexStringEnd;
            }
            case ForwardSlash:
            {
                Advance(1);
                return PdfTokenType.Name;
            }
            case LeftParen:
            {
                Advance(1);
                return PdfTokenType.String;
            }
            case RightParen:
            {
                Advance(1);
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
                if (current >= Zero && current <= Nine)
                {
                    return PdfTokenType.Number;
                }

                if (_allowReferences && current == Reference && IsTokenTerminator(PeekByte(1)))
                {
                    Advance(1);
                    return PdfTokenType.Reference;
                }

                // Check for ID operator (inline stream start)
                if (current == (byte)'I' && PeekByte(1) == (byte)'D' && IsTokenTerminator(PeekByte(2)))
                {
                    Advance(2); // Consume "ID"
                    return PdfTokenType.InlineStreamStart;
                }

                return PdfTokenType.Operator;
            }
        }
    }
}
