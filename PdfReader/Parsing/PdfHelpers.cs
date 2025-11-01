using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    // TODO: limit usage of this class where PdfParseContext can be used
    internal static class PdfParsingHelpers
    {
        // Constants
        public const byte Separator = (byte)' ';
        public const byte CarriageReturn = (byte)'\r';
        public const byte LineFeed = (byte)'\n';
        private const byte CommentStart = (byte)'%';

        // Character classification methods - called extremely frequently, aggressive inline
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWhitespace(byte b) => b == ' ' || b == '\t' || b == '\r' || b == '\n' || b == '\0' || b == '\f';
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDigit(byte b) => b >= PdfTokens.Zero && b <= PdfTokens.Nine;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHexDigit(byte b) => b >= '0' && b <= '9' || b >= 'A' && b <= 'F' || b >= 'a' && b <= 'f';
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDelimiter(byte b) => b == PdfTokens.LeftParen || b == PdfTokens.RightParen || 
            b == PdfTokens.LeftAngle || b == PdfTokens.RightAngle || b == PdfTokens.LeftSquare || b == PdfTokens.RightSquare || 
            b == '{' || b == '}' || b == PdfTokens.ForwardSlash || b == '%';

        // Buffer access methods - extremely hot path, aggressive inline
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(ref PdfParseContext context) => context.ReadByte();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte PeekByte(ref PdfParseContext context, int offset = 0) => context.PeekByte(offset); // TODO: remove

        // Navigation helpers
        public static void SkipWhitespaceAndComment(ref PdfParseContext context)
        {
            while (!context.IsAtEnd)
            {
                byte b = PeekByte(ref context);
                // Skip standard whitespace
                if (IsWhitespace(b))
                {
                    context.Advance(1);
                    continue;
                }

                // Skip PDF comments: '%' to end-of-line (CR or LF) when enabled
                if (b == CommentStart)
                {
                    // advance past '%'
                    context.Advance(1);
                    while (!context.IsAtEnd)
                    {
                        byte c = PeekByte(ref context);
                        if (c == CarriageReturn || c == LineFeed)
                        {
                            // consume EOL and break
                            context.Advance(1);
                            break;
                        }
                        context.Advance(1);
                    }
                    // continue outer loop to skip further whitespace/comments
                    continue;
                }

                // Non-whitespace, or comments not skipped
                break;
            }
        }

        /// <summary>
        /// Advance past exactly one end-of-line sequence if present at the current position.
        /// Recognizes CRLF, CR, or LF. If no EOL is present, does nothing.
        /// Use for cases like after the "stream" keyword where the spec allows a single EOL.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipSingleEol(ref PdfParseContext context)
        {
            if (context.IsAtEnd) return;
            byte b0 = PeekByte(ref context);
            if (b0 == CarriageReturn)
            {
                // CRLF pair
                if (context.Position + 1 < context.Length && PeekByte(ref context, 1) == LineFeed)
                {
                    context.Advance(2);
                }
                else
                {
                    context.Advance(1);
                }
            }
            else if (b0 == LineFeed)
            {
                context.Advance(1);
            }
        }

        // Unified token matching method - frequently used for operator recognition
        public static bool MatchSequence(ref PdfParseContext context, ReadOnlySpan<byte> sequence)
        {
            if (context.Position + sequence.Length > context.Length)
                return false;

            if (context.MatchSequenceAt(context.Position, sequence))
            {
                context.Advance(sequence.Length);
                return true;
            }

            return false;
        }
    }
}