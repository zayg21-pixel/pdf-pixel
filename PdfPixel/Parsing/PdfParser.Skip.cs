using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

partial struct PdfParser
{
    /// <summary>
    /// Checks if the specified byte is a PDF whitespace character.
    /// PDF whitespace includes: space, tab, CR, LF, form feed, and null.
    /// </summary>
    /// <param name="b">The byte to check.</param>
    /// <returns>True if the byte is a whitespace character; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespace(byte b)
    {
        return b == Separator ||    // space
               b == Tab ||          // tab
               b == CarriageReturn ||
               b == LineFeed ||
               b == FormFeed ||
               b == NullChar;       // null padding
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipWhitespacesAndComments()
    {
        while (!IsAtEnd)
        {
            byte current = PeekByte();
            switch (current)
            {
                case Separator: // space
                case Tab:       // tab
                case CarriageReturn:
                case LineFeed:
                case NullChar:  // null padding
                case FormFeed:  // form feed
                {
                    Advance(1);
                    continue;
                }
                case CommentStart:
                {
                    Advance(1); // consume '%'
                    while (!IsAtEnd)
                    {
                        byte commentChar = PeekByte();
                        if (commentChar == CarriageReturn || commentChar == LineFeed)
                        {
                            Advance(1); // consume EOL terminator
                            break;
                        }
                        Advance(1); // advance through comment content
                    }
                    continue; // resume outer loop
                }
                default:
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Skips a single end-of-line sequence in the input, if present.
    /// </summary>
    /// <remarks>This method advances the input position past a single end-of-line sequence, which can
    /// be either a carriage return ('\r'), a line feed ('\n'), or a carriage return followed by a line feed
    /// ("\r\n"). If the input is already at the end, no action is taken.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipSingleEndOfLine()
    {
        if (IsAtEnd)
        {
            return;
        }
        byte b = PeekByte();
        if (b == (byte)'\r')
        {
            Advance(1);
            if (!IsAtEnd && PeekByte() == (byte)'\n')
            {
                Advance(1);
            }
        }
        else if (b == (byte)'\n')
        {
            Advance(1);
        }
    }

    /// <summary>
    /// Checks if the specified byte is a PDF token terminator.
    /// PDF token terminators include whitespace characters and delimiter characters.
    /// Used for both names and operators.
    /// </summary>
    /// <param name="b">The byte to check.</param>
    /// <returns>True if the byte terminates a PDF token; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTokenTerminator(byte b)
    {
        switch (b)
        {
            // Whitespace characters terminate tokens
            case Separator:      // space
            case Tab:            // tab
            case CarriageReturn: // CR
            case LineFeed:       // LF
            case FormFeed:       // form feed
            case NullChar:       // null
            // Delimiter characters terminate tokens
            case ForwardSlash:   // / (starts another name)
            case LeftParen:      // ( (string start)
            case RightParen:     // ) (string end)
            case LeftAngle:      // < (hex string or dict start)
            case RightAngle:     // > (hex string or dict end)
            case LeftSquare:     // [ (array start)
            case RightSquare:    // ] (array end)
            case CommentStart:   // % (comment start)
                return true;
            default:
                return false;
        }
    }
}
