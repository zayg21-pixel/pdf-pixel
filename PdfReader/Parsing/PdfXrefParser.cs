using System;
using System.Runtime.CompilerServices;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles parsing of XRef tables and trailer dictionaries
    /// </summary>
    public static class PdfXrefParser
    {
        /// <summary>
        /// Find the startxref position in the PDF
        /// </summary>
        public static int FindStartXref(ref PdfParseContext context)
        {
            // Search backwards from the end, checking only at line beginnings
            return SearchStartxrefBackwardsOnNewLines(context.GetSlice(0, context.Length));
        }

        /// <summary>
        /// Parse XRef table and trailer dictionary
        /// </summary>
        public static void ParseXrefAndTrailer(ref PdfParseContext context, PdfDocument document, int xrefPosition)
        {
            context.Position = xrefPosition;
            
            // Skip "xref" if it exists
            if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Xref))
            {
                context.Advance(1);
            }
            
            // Read xref entries (simplified - we'll focus on trailer for now)
            // Skip to trailer
            while (context.Position < context.Length)
            {
                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Trailer))
                {
                    break;
                }
                context.Position++;
            }
            
            if (context.Position < context.Length)
            {
                context.Advance(1); // Skip "trailer"
                var trailerDict = PdfParsers.ParseDictionary(ref context, document, allowReferences: true);
                
                // When extracting Root reference from trailer dictionary:
                // var rootObject = trailerDict.GetPageObject(PdfTokens.RootKey);
                // if (rootObject != null) document.RootRef = rootObject.Reference.ObjectNumber;
                
                var rootObject = trailerDict.GetPageObject(PdfTokens.RootKey);
                if (rootObject != null)
                {
                    document.RootRef = rootObject.Reference.ObjectNumber;
                }
            }
        }

        private static int SearchStartxrefBackwardsOnNewLines(ReadOnlySpan<byte> buffer)
        {
            // Search for "startxref" moving backwards, checking only at line beginnings using SequenceEqual
            // This is the simplest and most reliable approach
            
            for (int i = buffer.Length - PdfTokens.Startxref.Length; i >= 0; i--)
            {
                // Check if we're at the start of the file or after a newline
                if (i == 0 || buffer[i - 1] == PdfParsingHelpers.LineFeed || buffer[i - 1] == PdfParsingHelpers.CarriageReturn)
                {
                    // Use SequenceEqual for simple and reliable comparison
                    if (buffer.Slice(i, PdfTokens.Startxref.Length).SequenceEqual(PdfTokens.Startxref))
                    {
                        return ExtractXrefPosition(buffer, i + PdfTokens.Startxref.Length);
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Optimized xref position extraction using direct span parsing (no PdfParseContext overhead)
        /// XRef positions are always positive integers, so no sign handling needed
        /// </summary>
        private static int ExtractXrefPosition(ReadOnlySpan<byte> buffer, int startPos)
        {
            var slice = buffer.Slice(startPos);
            
            // Skip whitespace directly on span using PdfHelpers
            int position = 0;
            while (position < slice.Length && PdfParsingHelpers.IsWhitespace(slice[position]))
                position++;
            
            // Parse positive integer directly from span - ultra fast!
            if (TryParsePositiveIntegerFromSpan(slice, ref position, out int xrefPos))
            {
                return xrefPos;
            }
            
            return -1;
        }

        /// <summary>
        /// Ultra-fast positive integer parsing directly from span without any allocation or context overhead
        /// Optimized for XRef positions which are always positive - aggressive inline for maximum performance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParsePositiveIntegerFromSpan(ReadOnlySpan<byte> span, ref int position, out int number)
        {
            number = 0;
            bool hasDigits = false;
            
            if (position >= span.Length)
                return false;
            
            // Skip optional '+' sign (xref positions are always positive)
            if (span[position] == PdfTokens.Plus)
                position++;
            
            // Parse digits using PdfHelpers for consistency
            while (position < span.Length && PdfParsingHelpers.IsDigit(span[position]))
            {
                hasDigits = true;
                number = number * 10 + (span[position] - PdfTokens.Zero);
                position++;
            }
            
            return hasDigits;
        }
    }
}