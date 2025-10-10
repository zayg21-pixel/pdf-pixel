using PdfReader.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using PdfReader.Text;

namespace PdfReader.Parsing
{
    public static class PdfParsers
    {
        public static bool TryParseObjectHeader(ref PdfParseContext context, out int objNum, out int generation)
        {
            objNum = 0;
            generation = 0;
            
            int startPos = context.Position;
            
            // Try to parse: number generation obj
            if (!TryParseNumber(ref context, out objNum))
            {
                context.Position = startPos;
                return false;
            }
            
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            
            if (!TryParseNumber(ref context, out generation))
            {
                context.Position = startPos;
                return false;
            }
            
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            
            if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Obj))
            {
                context.Position = startPos;
                return false;
            }
            
            return true;
        }

        public static IPdfValue ParsePdfValue(ref PdfParseContext context, PdfDocument document, PdfReference targetReference = default, bool allowReferences = false, bool shouldDecrypt = false)
        {
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

            if (context.IsAtEnd)
            {
                return null;
            }

            byte b = PdfParsingHelpers.PeekByte(ref context);

            if (b == PdfTokens.ForwardSlash)
            {
                var name = ParseNameAsString(ref context);
                return name != null ? PdfValue.Name(name) : null;
            }
            else if (b == PdfTokens.LeftSquare)
            {
                var array = ParsePdfArray(ref context, document, targetReference, allowReferences, shouldDecrypt);
                return array != null ? PdfValue.Array(array) : null;
            }
            else if (b == PdfTokens.LeftAngle && PdfParsingHelpers.PeekByte(ref context, 1) == PdfTokens.LeftAngle)
            {
                var subDict = ParseDictionary(ref context, document, targetReference, allowReferences, shouldDecrypt);
                return PdfValue.Dictionary(subDict);
            }
            else if (b == PdfTokens.LeftAngle)
            {
                // Single angle bracket - hex string. Do not decrypt here (spec encryption applies to the raw bytes; hex literal stays hex digits).
                var hexString = ParseHexStringAsHexDigits(ref context);
                return hexString != null ? PdfValue.HexString(hexString) : null;
            }
            else if (PdfParsingHelpers.IsDigit(b) || b == PdfTokens.Minus || b == PdfTokens.Plus || b == PdfTokens.Dot)
            {
                return ParseNumericValue(ref context, allowReferences);
            }
            else if (b == PdfTokens.LeftParen)
            {
                var str = ParseStringAsString(ref context);
                if (str == null)
                {
                    return null;
                }

                str = GetDecryptedString(document, targetReference, shouldDecrypt, str);

                return PdfValue.String(str);
            }

            var token = ParseTokenAsString(ref context);
            return token != null ? PdfValue.Operator(token) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetDecryptedString(PdfDocument document, PdfReference reference, bool shouldDecrypt, string str)
        {
            if (shouldDecrypt && document.Decryptor != null)
            {
                try
                {
                    // Interpret parsed string as ISO-8859-1 bytes (baseline PDF doc encoding) and decrypt.
                    byte[] rawBytes = EncodingExtensions.PdfDefault.GetBytes(str);
                    var decrypted = document.Decryptor.DecryptBytes(rawBytes, reference);
                    str = EncodingExtensions.PdfDefault.GetString(decrypted);
                }
                catch
                {
                    // Ignore decryption errors at this stage (leave original string).
                }
            }

            return str;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IPdfValue ParseNumericValue(ref PdfParseContext context, bool allowReferences)
        {
            // Save position to handle reference parsing correctly
            int startPos = context.Position;
            
            // Check if this could be a decimal starting with a dot
            byte firstByte = PdfParsingHelpers.PeekByte(ref context);
            bool startsWithDot = firstByte == PdfTokens.Dot;
            
            // Try to parse as float first to handle decimals properly
            if (TryParseFloat(ref context, out float floatVal))
            {
                // Check if this was actually an integer (no decimal point consumed)
                bool isActuallyInteger = !startsWithDot && floatVal == (int)floatVal;
                
                if (isActuallyInteger)
                {
                    int intVal = (int)floatVal;
                    // Try to parse a second number for potential reference
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                    int afterFirstNumber = context.Position;
                    
                    if (allowReferences && TryParseNumber(ref context, out int gen))
                    {
                        PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                        if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.R))
                        {
                            // Successfully parsed reference
                            return PdfValue.Reference(new PdfReference(intVal, gen));
                        }
                        else
                        {
                            // Not a reference, backtrack to after first number
                            context.Position = afterFirstNumber;
                        }
                    }
                    
                    return PdfValue.Integer(intVal);
                }
                else
                {
                    // This is definitely a float (has decimal part or starts with dot)
                    return PdfValue.Real(floatVal);
                }
            }
            
            // Reset position if parsing failed
            context.Position = startPos;
            return null;
        }

        private static PdfDictionary ParseDictionary(ref PdfParseContext context, PdfDocument document, PdfReference targetReference, bool allowReferences, bool shouldDecrypt)
        {
            var dict = new PdfDictionary(document);

            if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.DictStart))
            {
                return dict;
            }

            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.DictEnd))
                {
                    break;
                }

                // Parse key
                if (PdfParsingHelpers.PeekByte(ref context) != PdfTokens.ForwardSlash)
                {
                    break;
                }

                string key = ParseNameAsString(ref context);
                if (string.IsNullOrEmpty(key))
                {
                    break;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                // Parse value
                var value = ParsePdfValue(ref context, document, targetReference, allowReferences, shouldDecrypt);
                if (value != null)
                {
                    dict.Set(key, value);
                }
            }

            return dict;
        }

        private static PdfArray ParsePdfArray(ref PdfParseContext context, PdfDocument document, PdfReference targetReference, bool allowReferences, bool shouldDecrypt)
        {
            var array = new List<IPdfValue>();
            
            if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.ArrayStart))
            {
                return new PdfArray(document, array);
            }
            
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                
                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.ArrayEnd))
                {
                    break;
                }
                
                var value = ParsePdfValue(ref context, document, targetReference, allowReferences, shouldDecrypt);
                if (value != null)
                {
                    array.Add(value);
                }
                else
                {
                    // If we can't parse a value, skip ahead to avoid infinite loop
                    context.Advance(1);
                }
            }
            
            return new PdfArray(document, array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ParseNameAsString(ref PdfParseContext context)
        {
            if (PdfParsingHelpers.PeekByte(ref context) != PdfTokens.ForwardSlash)
            {
                return null;
            }
            
            int start = context.Position;
            context.Advance(1); // Skip '/'
            
            while (!context.IsAtEnd)
            {
                byte b = PdfParsingHelpers.PeekByte(ref context);
                if (PdfParsingHelpers.IsWhitespace(b) || b == PdfTokens.ForwardSlash || 
                    b == PdfTokens.LeftSquare || b == PdfTokens.RightSquare || 
                    b == PdfTokens.LeftAngle || b == PdfTokens.RightAngle || 
                    b == PdfTokens.LeftParen || b == PdfTokens.RightParen)
                {
                    break;
                }
                
                context.Advance(1);
            }
            
            // PDF spec: Names may contain any characters except null (0) encoded as #XX
            return DecodePdfName(context.GetSlice(start, context.Position - start));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string DecodePdfName(ReadOnlySpan<byte> nameBytes)
        {
            var result = new StringBuilder(nameBytes.Length); // Pre-allocate for efficiency
            
            for (int i = 0; i < nameBytes.Length; i++)
            {
                if (nameBytes[i] == (byte)'#' && i + 2 < nameBytes.Length)
                {
                    // Try to parse the next two bytes as hex digits
                    byte hex1 = nameBytes[i + 1];
                    byte hex2 = nameBytes[i + 2];
                    
                    if (IsHexDigit(hex1) && IsHexDigit(hex2))
                    {
                        // Convert hex pair to ASCII character value
                        int charValue = (HexDigitToValue(hex1) << 4) | HexDigitToValue(hex2);
                        result.Append((char)charValue);
                        i += 2; // Skip the hex digits
                    }
                    else
                    {
                        // Not valid hex, keep the # character
                        result.Append('#');
                    }
                }
                else
                {
                    // Regular ASCII character
                    result.Append((char)nameBytes[i]);
                }
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Check if a byte represents a valid hexadecimal digit (0-9, A-F, a-f)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexDigit(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9') ||
                   (b >= (byte)'A' && b <= (byte)'F') ||
                   (b >= (byte)'a' && b <= (byte)'f');
        }

        /// <summary>
        /// Convert a hex digit byte to its numeric value (0-15)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexDigitToValue(byte hexDigit)
        {
            if (hexDigit >= (byte)'0' && hexDigit <= (byte)'9')
            {
                return hexDigit - (byte)'0';
            }
            else if (hexDigit >= (byte)'A' && hexDigit <= (byte)'F')
            {
                return hexDigit - (byte)'A' + 10;
            }
            else if (hexDigit >= (byte)'a' && hexDigit <= (byte)'f')
            {
                return hexDigit - (byte)'a' + 10;
            }
            else
            {
                return 0; // Should never happen if IsHexDigit was called first
            }
        }

        private static string ParseStringAsString(ref PdfParseContext context)
        {
            if (PdfParsingHelpers.PeekByte(ref context) != PdfTokens.LeftParen)
            {
                return null;
            }
            
            context.Position++; // Skip '('
            
            var str = new StringBuilder();
            int parenCount = 1;
            
            while (context.Position < context.Length && parenCount > 0)
            {
                byte b = PdfParsingHelpers.ReadByte(ref context);
                
                if (b == PdfTokens.LeftParen)
                {
                    parenCount++;
                    str.Append((char)b);
                }
                else if (b == PdfTokens.RightParen)
                {
                    parenCount--;
                    if (parenCount > 0)
                    {
                        str.Append((char)b);
                    }
                }
                else if (b == PdfTokens.Backslash && context.Position < context.Length)
                {
                    // Handle PDF escape sequences
                    HandleEscapedSequence(ref context, str);
                    continue;
                }
                else
                {
                    str.Append((char)b);
                }
            }
            
            return str.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HandleEscapedSequence(ref PdfParseContext context, StringBuilder builder)
        {
            if (context.Position >= context.Length)
            {
                return;
            }

            byte next = PdfParsingHelpers.ReadByte(ref context);

            // Line continuation: a backslash followed by EOL (CR, LF, or CRLF) -> ignore the EOL entirely.
            if (next == (byte)'\n')
            {
                return;
            }
            if (next == (byte)'\r')
            {
                if (context.Position < context.Length && PdfParsingHelpers.PeekByte(ref context) == (byte)'\n')
                {
                    context.Advance(1); // Consume optional LF following CR.
                }
                return;
            }

            // Octal escape: up to 3 octal digits (0-7), value 0..255.
            if (next >= (byte)'0' && next <= (byte)'7')
            {
                int value = next - (byte)'0';
                for (int digitIndex = 0; digitIndex < 2 && context.Position < context.Length; digitIndex++)
                {
                    byte candidate = PdfParsingHelpers.PeekByte(ref context);
                    if (candidate < (byte)'0' || candidate > (byte)'7')
                    {
                        break;
                    }
                    context.Advance(1);
                    value = (value << 3) + (candidate - (byte)'0');
                }
                builder.Append((char)(value & 0xFF));
                return;
            }

            char mapped;
            switch (next)
            {
                case (byte)'n':
                    mapped = '\n';
                    break;
                case (byte)'r':
                    mapped = '\r';
                    break;
                case (byte)'t':
                    mapped = '\t';
                    break;
                case (byte)'b':
                    mapped = '\b';
                    break;
                case (byte)'f':
                    mapped = '\f';
                    break;
                case (byte)'(':
                    mapped = '(';
                    break;
                case (byte)')':
                    mapped = ')';
                    break;
                case (byte)'\\':
                    mapped = '\\';
                    break;
                default:
                    mapped = (char)next;
                    break;
            }

            builder.Append(mapped);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ParseTokenAsString(ref PdfParseContext context)
        {
            int start = context.Position;
            
            while (!context.IsAtEnd)
            {
                byte b = PdfParsingHelpers.PeekByte(ref context);
                if (PdfParsingHelpers.IsWhitespace(b) || PdfParsingHelpers.IsDelimiter(b))
                {
                    break;
                }
                
                context.Advance(1);
            }
            
            if (context.Position > start)
            {
                var span = context.GetSlice(start, context.Position - start);
                return EncodingExtensions.PdfDefault.GetString(span);
            }
            
            return null;
        }

        /// <summary>
        /// Parse a PDF stream using length-based approach when possible, falling back to endstream search.
        /// According to PDF specification, streams should have a /Length entry that specifies the exact number of bytes.
        /// This is much more reliable than searching for "endstream" keyword.
        /// </summary>
        /// <param name="context">PDF parse context positioned after "stream" keyword</param>
        /// <param name="streamDictionary">Stream object's dictionary containing /Length entry (optional)</param>
        /// <returns>Stream data as Memory&lt;byte&gt;</returns>
        public static ReadOnlyMemory<byte> ParseStream(ref PdfParseContext context, PdfDictionary streamDictionary)
        {
            // After the "stream" keyword, consume exactly one end-of-line if present per spec (CRLF, CR, or LF)
            PdfParsingHelpers.SkipSingleEol(ref context);

            int streamStart = context.Position;
            int streamLength = streamDictionary.GetIntegerOrDefault(PdfTokens.LengthKey);

            if (streamLength == 0)
            {
                int scanStart = context.Position;

                while (context.Position + PdfTokens.Endstream.Length <= context.Length)
                {
                    if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Endstream))
                    {
                        int endMarkerStart = context.Position - PdfTokens.Endstream.Length;
                        streamLength = endMarkerStart - scanStart;
                        context.Position = scanStart; // Reset position to start of stream data
                        break;
                    }
                    context.Advance(1);
                }

                while (streamLength > 0)
                {
                    byte last = context.PeekByte(streamLength - 1);
                    if (!PdfParsingHelpers.IsWhitespace(last))
                    {
                        break;
                    }
                    streamLength--;
                }
            }

            if (streamLength == 0)
            {
                // Unable to determine stream length - malformed PDF
                context.Position++;
                return ReadOnlyMemory<byte>.Empty;
            }

            // Validate that we have enough data
            int availableData = context.Length - streamStart;

            if (streamLength <= availableData)
            {
                ReadOnlyMemory<byte> result;

                if (context.IsSingleMemory)
                {
                    // Zero-copy: slice original backing memory
                    result = context.OriginalMemory.Slice(streamStart, streamLength);
                }
                else
                {
                    // should never happen as for stream parsing we're having single memory, but handle gracefully
                    result = context.GetSlice(streamStart, streamLength).ToArray();
                }

                context.Position = streamStart + streamLength;

                // Skip past optional EOL/whitespace/comments before endstream
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (context.Position + PdfTokens.Endstream.Length <= context.Length)
                {
                    if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Endstream))
                    {
                        // malformed PDF: no "endstream" found
                        return ReadOnlyMemory<byte>.Empty;
                    }
                }

                return result;
            }
            else
            {
                // malformed PDF: specified stream length exceeds available data
                context.Position++;
                return ReadOnlyMemory<byte>.Empty;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseNumber(ref PdfParseContext context, out int number)
        {
            number = 0;
            bool negative = false;
            bool hasDigits = false;
            
            if (PdfParsingHelpers.PeekByte(ref context) == PdfTokens.Minus)
            {
                negative = true;
                context.Advance(1);
            }
            else if (PdfParsingHelpers.PeekByte(ref context) == PdfTokens.Plus)
            {
                context.Advance(1);
            }
            
            while (!context.IsAtEnd && PdfParsingHelpers.IsDigit(PdfParsingHelpers.PeekByte(ref context)))
            {
                hasDigits = true;
                number = number * 10 + (PdfParsingHelpers.PeekByte(ref context) - PdfTokens.Zero);
                context.Advance(1);
            }
            
            if (hasDigits)
            {
                if (negative)
                {
                    number = -number;
                }
                return true;
            }
            
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseFloat(ref PdfParseContext context, out float number)
        {
            number = 0f;
            int startPos = context.Position;
            bool negative = false;
            bool hasDigits = false;
            
            // Handle sign
            if (PdfParsingHelpers.PeekByte(ref context) == PdfTokens.Minus)
            {
                negative = true;
                context.Advance(1);
            }
            else if (PdfParsingHelpers.PeekByte(ref context) == PdfTokens.Plus)
            {
                context.Advance(1);
            }
            
            // Parse integer part (if any)
            while (!context.IsAtEnd && PdfParsingHelpers.IsDigit(PdfParsingHelpers.PeekByte(ref context)))
            {
                hasDigits = true;
                number = number * 10 + (PdfParsingHelpers.PeekByte(ref context) - PdfTokens.Zero);
                context.Advance(1);
            }
            
            // Handle decimal point and fractional part
            if (!context.IsAtEnd && PdfParsingHelpers.PeekByte(ref context) == PdfTokens.Dot)
            {
                context.Advance(1); // Skip the dot
                float fractional = 0f;
                float divisor = 10f;
                
                while (!context.IsAtEnd && PdfParsingHelpers.IsDigit(PdfParsingHelpers.PeekByte(ref context)))
                {
                    hasDigits = true; // Mark as having digits if we have fractional digits
                    fractional += (PdfParsingHelpers.PeekByte(ref context) - PdfTokens.Zero) / divisor;
                    divisor *= 10f;
                    context.Advance(1);
                }
                
                number += fractional;
            }
            
            if (hasDigits)
            {
                if (negative)
                {
                    number = -number;
                }
                return true;
            }
            
            // Reset position if parsing failed
            context.Position = startPos;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ParseHexStringAsHexDigits(ref PdfParseContext context)
        {
            if (PdfParsingHelpers.PeekByte(ref context) != PdfTokens.LeftAngle)
            {
                return null;
            }
            
            context.Advance(1); // Skip '<'
            
            var hexDigits = new StringBuilder();
            
            while (!context.IsAtEnd)
            {
                byte b = PdfParsingHelpers.PeekByte(ref context);
                
                if (b == PdfTokens.RightAngle)
                {
                    context.Advance(1); // Skip '>'
                    break;
                }
                else if (PdfParsingHelpers.IsWhitespace(b))
                {
                    context.Advance(1); // Skip whitespace
                    continue;
                }
                else if (PdfParsingHelpers.IsHexDigit(b))
                {
                    // Preserve hex digits as characters
                    hexDigits.Append((char)b);
                    context.Advance(1);
                }
                else
                {
                    // Invalid character, skip it
                    context.Advance(1);
                }
            }
            
            return hexDigits.ToString();
        }
    }
}