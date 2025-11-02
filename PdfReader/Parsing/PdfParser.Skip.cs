using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
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
        private int SkipWhitespacesAndComments()
        {
            int startPosition = _parseContext.Position; // capture starting offset
            while (!_parseContext.IsAtEnd)
            {
                byte current = _parseContext.PeekByte();
                switch (current)
                {
                    case Separator: // space
                    case Tab:       // tab
                    case CarriageReturn:
                    case LineFeed:
                    case NullChar:  // null padding
                    case FormFeed:  // form feed
                    {
                        _parseContext.Advance(1);
                        continue;
                    }
                    case CommentStart:
                    {
                        _parseContext.Advance(1); // consume '%'
                        while (!_parseContext.IsAtEnd)
                        {
                            byte commentChar = _parseContext.PeekByte();
                            if (commentChar == CarriageReturn || commentChar == LineFeed)
                            {
                                _parseContext.Advance(1); // consume EOL terminator
                                break;
                            }
                            _parseContext.Advance(1); // advance through comment content
                        }
                        continue; // resume outer loop
                    }
                    default:
                    {
                        return _parseContext.Position - startPosition; // number of skipped bytes
                    }
                }
            }
            return _parseContext.Position - startPosition;
        }

        /// <summary>
        /// Skip only PDF whitespace characters (space, tab, CR, LF, form feed, null) without processing comments.
        /// Returns number of skipped bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SkipWhitespaces()
        {
            int startPosition = _parseContext.Position;
            while (!_parseContext.IsAtEnd)
            {
                byte current = _parseContext.PeekByte();
                switch (current)
                {
                    case Separator: // space
                    case Tab:       // tab
                    case CarriageReturn:
                    case LineFeed:
                    case NullChar:  // null padding
                    case FormFeed:  // form feed
                    {
                        _parseContext.Advance(1);
                        continue;
                    }
                    default:
                    {
                        return _parseContext.Position - startPosition;
                    }
                }
            }
            return _parseContext.Position - startPosition;
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
}
