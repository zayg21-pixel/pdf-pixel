using System;
using System.Runtime.CompilerServices;

namespace PdfReader
{
    internal static class PdfHelpers
    {
        // Constants
        public const byte Separator = (byte)' ';
        public const byte CarriageReturn = (byte)'\r';
        public const byte LineFeed = (byte)'\n';
        private const byte CommentStart = (byte)'%';

        // Cache for Boyer-Moore bad character tables using struct keys for zero allocation
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<PatternKey, int[]> BadCharTableCache 
            = new System.Collections.Concurrent.ConcurrentDictionary<PatternKey, int[]>();

        // Character classification methods - called extremely frequently, aggressive inline
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWhitespace(byte b) => b == ' ' || b == '\t' || b == '\r' || b == '\n' || b == '\0' || b == '\f';
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDigit(byte b) => b >= PdfTokens.Zero && b <= PdfTokens.Nine;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHexDigit(byte b) => (b >= '0' && b <= '9') || (b >= 'A' && b <= 'F') || (b >= 'a' && b <= 'f');
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDelimiter(byte b) => b == PdfTokens.LeftParen || b == PdfTokens.RightParen || 
            b == PdfTokens.LeftAngle || b == PdfTokens.RightAngle || b == PdfTokens.LeftSquare || b == PdfTokens.RightSquare || 
            b == '{' || b == '}' || b == PdfTokens.ForwardSlash || b == '%';

        // Buffer access methods - extremely hot path, aggressive inline
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(ref PdfParseContext context) => context.ReadByte();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte PeekByte(ref PdfParseContext context, int offset = 0) => context.PeekByte(offset);

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

        /// <summary>
        /// Advance past a single space or tab byte if present. Do not consume newlines.
        /// Useful after inline image "ID" marker which requires exactly one white-space character.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipSingleSpaceOrTab(ref PdfParseContext context)
        {
            if (context.IsAtEnd) return;
            byte b0 = PeekByte(ref context);
            if (b0 == (byte)' ' || b0 == (byte)'\t')
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

        /// <summary>
        /// Optimized pattern search using Boyer-Moore bad character heuristic with caching
        /// Returns the position where the pattern is found, or -1 if not found
        /// </summary>
        public static int FindPatternFromPosition(ref PdfParseContext context, int startPosition, ReadOnlySpan<byte> pattern)
        {
            if (pattern.Length == 0)
                return startPosition;
            
            if (startPosition + pattern.Length > context.Length)
                return -1;

            for (int i = startPosition; i < context.Length; i++)
            {
                if (context.MatchSequenceAt(i, pattern))
                {
                    return i;
                }
            }

            return -1;

            // TODO: version below does not work correctly - needs fixing and testing

            // Get or create bad character table for this pattern
            var badCharTable = GetOrCreateBadCharTable(pattern);


            return FindPatternWithBadCharTable(ref context, startPosition, context.Length - startPosition, pattern, badCharTable);
        }

        /// <summary>
        /// Get or create a cached bad character table using zero-allocation struct key
        /// </summary>
        private static int[] GetOrCreateBadCharTable(ReadOnlySpan<byte> pattern)
        {
            var key = new PatternKey(pattern);
            
            // Check if we already have this pattern cached
            if (BadCharTableCache.TryGetValue(key, out int[] existingTable))
            {
                return existingTable;
            }
            
            // Create new table and cache it
            var newTable = CreateBadCharTable(pattern);
            BadCharTableCache.TryAdd(key, newTable);
            return newTable;
        }

        /// <summary>
        /// Create Boyer-Moore bad character table for a pattern
        /// </summary>
        private static int[] CreateBadCharTable(ReadOnlySpan<byte> pattern)
        {
            var table = new int[256];
            int patternLength = pattern.Length;
            
            // Initialize all characters to pattern length (worst case)
            for (int i = 0; i < 256; i++)
                table[i] = patternLength;
                
            // Set actual distances for characters in the pattern
            for (int i = 0; i < patternLength - 1; i++)
                table[i] = patternLength - 1 - i;
                
            return table;
        }

        /// <summary>
        /// Core Boyer-Moore search implementation with bad character heuristic
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindPatternWithBadCharTable(ref PdfParseContext context, int start, int maxLength, ReadOnlySpan<byte> pattern, int[] badCharTable)
        {
            int patternLength = pattern.Length;
            int searchEnd = start + maxLength - patternLength;
            int pos = start;
            
            while (pos <= searchEnd)
            {
                // Check if we're about to go out of bounds
                if (pos + patternLength > context.Length)
                    break;
                
                // Optimize for common patterns by checking last character first
                byte lastChar = context.PeekByte(pos + patternLength - 1);
                if (lastChar != pattern[patternLength - 1])
                {
                    // Use bad character heuristic for quick skip
                    pos += badCharTable[lastChar];
                    continue;
                }
                
                // Check first character to avoid unnecessary full comparison
                if (context.PeekByte(pos) != pattern[0])
                {
                    pos++;
                    continue;
                }
                
                // Do full pattern match from left to right
                int j = 0;
                while (j < patternLength && context.PeekByte(pos + j) == pattern[j])
                    j++;
                
                if (j == patternLength)
                {
                    // Pattern found!
                    return pos;
                }
                else
                {
                    // Pattern not found, use bad character heuristic to skip ahead
                    byte mismatchChar = context.PeekByte(pos + j);
                    int skip = badCharTable[mismatchChar];
                    pos += Math.Max(1, skip - (patternLength - 1 - j));
                }
            }
            
            return -1; // Pattern not found
        }
    }
}