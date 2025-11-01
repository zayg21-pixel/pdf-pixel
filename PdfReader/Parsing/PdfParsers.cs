using PdfReader.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
                return name.IsEmpty ? null : PdfValue.Name(name);
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
                var hexString = ParseHexStringAsString(ref context);
                return hexString.IsEmpty ? null : PdfValue.String(hexString);
            }
            else if (PdfParsingHelpers.IsDigit(b) || b == PdfTokens.Minus || b == PdfTokens.Plus || b == PdfTokens.Dot)
            {
                return ParseNumericValue(ref context, allowReferences);
            }
            else if (b == PdfTokens.LeftParen)
            {
                var str = ParseStringAsString(ref context);
                if (str.IsEmpty)
                {
                    return null;
                }

                str = GetDecryptedString(document, targetReference, shouldDecrypt, str);

                return PdfValue.String(str);
            }

            var token = ParseTokenAsString(ref context);

            if (token == PdfTokens.TrueValue)
            {
                return PdfValue.Boolean(true);
            }
            else if (token == PdfTokens.FalseValue)
            {
                return PdfValue.Boolean(false);
            }

            return !token.IsEmpty ? PdfValue.Operator(token) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PdfString GetDecryptedString(PdfDocument document, PdfReference reference, bool shouldDecrypt, PdfString str)
        {
            if (shouldDecrypt && document.Decryptor != null)
            {
                try
                {
                    // Interpret parsed string as ISO-8859-1 bytes (baseline PDF doc encoding) and decrypt.
                    var decrypted = document.Decryptor.DecryptBytes(str.Value, reference);
                    return new PdfString(decrypted);
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
            
            // Try to parse as float first to handle decimals properly
            if (TryParseFloat(ref context, out float floatVal))
            {
                // Check if this was actually an integer (no decimal point consumed)
                bool isActuallyInteger = floatVal == (int)floatVal;
                
                if (isActuallyInteger)
                {
                    int intVal = (int)floatVal;
                    // Try to parse a second number for potential reference
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                    int afterFirstNumber = context.Position;
                    
                    if (allowReferences && TryParseNumber(ref context, out int gen))
                    {
                        PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                        if (context.PeekByte() == PdfTokens.Reference)
                        {
                            context.Advance(1); // Consume 'R'

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

                PdfString keyString = ParseNameAsString(ref context);
                if (keyString.IsEmpty)
                {
                    break;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                // Parse value
                var value = ParsePdfValue(ref context, document, targetReference, allowReferences, shouldDecrypt);
                if (value != null)
                {
                    dict.Set(keyString, value);
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
        private static PdfString ParseNameAsString(ref PdfParseContext context)
        {
            // Check for name start
            if (context.PeekByte() != PdfTokens.ForwardSlash)
            {
                return default;
            }

            var nameBytes = new List<byte>();
            context.Advance(1); // Skip '/'

            while (!context.IsAtEnd)
            {
                byte currentByte = context.PeekByte();
                if (PdfParsingHelpers.IsWhitespace(currentByte) ||
                    currentByte == PdfTokens.ForwardSlash ||
                    currentByte == PdfTokens.LeftSquare ||
                    currentByte == PdfTokens.RightSquare ||
                    currentByte == PdfTokens.LeftAngle ||
                    currentByte == PdfTokens.RightAngle ||
                    currentByte == PdfTokens.LeftParen ||
                    currentByte == PdfTokens.RightParen)
                {
                    break;
                }

                // Hex escape: #XX
                if (currentByte == (byte)'#')
                {
                    // Check if there are at least two more bytes for hex escape
                    if (!context.IsAtEnd && context.PeekByte(1) != 0 && context.PeekByte(2) != 0)
                    {
                        context.ReadByte(); // Skip '#'
                        byte hex1 = context.ReadByte();
                        byte hex2 = context.ReadByte();
                        if (IsHexDigit(hex1) && IsHexDigit(hex2))
                        {
                            int value = (HexDigitToValue(hex1) << 4) | HexDigitToValue(hex2);
                            nameBytes.Add((byte)value);
                        }
                        else
                        {
                            // Not valid hex, keep the '#' and the next two bytes as-is
                            nameBytes.Add((byte)'#');
                            nameBytes.Add(hex1);
                            nameBytes.Add(hex2);
                        }
                    }
                    else
                    {
                        // Not enough bytes for hex escape, treat as literal '#'
                        nameBytes.Add(context.ReadByte());
                    }
                }
                else
                {
                    nameBytes.Add(context.ReadByte());
                }
            }

            return new PdfString(nameBytes);
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

        private static PdfString ParseStringAsString(ref PdfParseContext context)
        {
            if (PdfParsingHelpers.PeekByte(ref context) != PdfTokens.LeftParen)
            {
                return default;
            }
            
            context.Position++; // Skip '('
            
            List<byte> stringBytes = new List<byte>();
            int parenCount = 1;
            
            while (context.Position < context.Length && parenCount > 0)
            {
                byte b = PdfParsingHelpers.ReadByte(ref context);
                
                if (b == PdfTokens.LeftParen)
                {
                    parenCount++;
                    stringBytes.Add(b);
                }
                else if (b == PdfTokens.RightParen)
                {
                    parenCount--;
                    if (parenCount > 0)
                    {
                        stringBytes.Add(b);
                    }
                }
                else if (b == PdfTokens.Backslash && context.Position < context.Length)
                {
                    // Handle PDF escape sequences
                    HandleEscapedSequence(ref context, stringBytes);
                    continue;
                }
                else
                {
                    stringBytes.Add(b);
                }
            }
            
            return new PdfString(stringBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HandleEscapedSequence(ref PdfParseContext context, List<byte> stringBytes)
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
                stringBytes.Add((byte)(value & 0xFF));
                return;
            }

            byte mapped;

            switch (next)
            {
                case (byte)'n':
                    mapped = (byte)'\n';
                    break;
                case (byte)'r':
                    mapped = (byte)'\r';
                    break;
                case (byte)'t':
                    mapped = (byte)'\t';
                    break;
                case (byte)'b':
                    mapped = (byte)'\b';
                    break;
                case (byte)'f':
                    mapped = (byte)'\f';
                    break;
                case (byte)'(':
                    mapped = (byte)'(';
                    break;
                case (byte)')':
                    mapped = (byte)')';
                    break;
                case (byte)'\\':
                    mapped = (byte)'\\';
                    break;
                default:
                    mapped = next;
                    break;
            }

            stringBytes.Add(mapped);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PdfString ParseTokenAsString(ref PdfParseContext context)
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
                return new PdfString(context.GetSlice(start, context.Position - start));
            }
            
            return default;
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
            // Parse sign
            int startPosition = context.Position;
            number = 0f;
            bool isNegative = false;
            
            byte firstByte = PdfParsingHelpers.PeekByte(ref context);
            if (firstByte == PdfTokens.Minus)
            {
                isNegative = true;
                context.Advance(1);
            }
            else if (firstByte == PdfTokens.Plus)
            {
                context.Advance(1);
            }

            // Parse integer part
            long integerPart = 0;
            bool hasDigits = false;
            while (!context.IsAtEnd && PdfParsingHelpers.IsDigit(PdfParsingHelpers.PeekByte(ref context)))
            {
                hasDigits = true;
                integerPart = integerPart * 10 + (PdfParsingHelpers.PeekByte(ref context) - PdfTokens.Zero);
                context.Advance(1);
            }

            // Parse fractional part if dot is present
            long fractionalPart = 0;
            int fractionalDigits = 0;
            if (!context.IsAtEnd && PdfParsingHelpers.PeekByte(ref context) == PdfTokens.Dot)
            {
                context.Advance(1); // Skip dot
                while (!context.IsAtEnd && PdfParsingHelpers.IsDigit(PdfParsingHelpers.PeekByte(ref context)))
                {
                    hasDigits = true;
                    fractionalPart = fractionalPart * 10 + (PdfParsingHelpers.PeekByte(ref context) - PdfTokens.Zero);
                    fractionalDigits++;
                    context.Advance(1);
                }
            }

            if (hasDigits)
            {
                float value = integerPart;
                if (fractionalDigits > 0)
                {
                    value += fractionalPart / (float)Math.Pow(10, fractionalDigits);
                }
                if (isNegative)
                {
                    value = -value;
                }
                number = value;
                return true;
            }

            // Reset position if parsing failed
            context.Position = startPosition;
            return false;
        }

        /// <summary>
        /// Parses a PDF hex string (e.g., <48656C6C6F>) and decodes it to a string using PDF default encoding.
        /// Decoding is performed live; odd-length hex strings are padded with zero as per PDF spec.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PdfString ParseHexStringAsString(ref PdfParseContext context)
        {
            if (PdfParsingHelpers.PeekByte(ref context) != PdfTokens.LeftAngle)
            {
                return default;
            }

            context.Advance(1); // Skip '<'

            var bytes = new List<byte>();
            int? highNibble = null;

            while (!context.IsAtEnd)
            {
                byte currentByte = PdfParsingHelpers.PeekByte(ref context);

                if (currentByte == PdfTokens.RightAngle)
                {
                    context.Advance(1); // Skip '>'
                    break;
                }
                else if (PdfParsingHelpers.IsWhitespace(currentByte))
                {
                    context.Advance(1); // Skip whitespace
                    continue;
                }
                else if (PdfParsingHelpers.IsHexDigit(currentByte))
                {
                    int value = HexDigitToValue(currentByte);
                    if (highNibble == null)
                    {
                        highNibble = value;
                    }
                    else
                    {
                        bytes.Add((byte)((highNibble.Value << 4) | value));
                        highNibble = null;
                    }
                    context.Advance(1);
                }
                else
                {
                    // Invalid character, skip it
                    context.Advance(1);
                }
            }

            // If odd number of hex digits, pad last nibble with zero
            if (highNibble != null)
            {
                bytes.Add((byte)(highNibble.Value << 4));
            }

            // Decode bytes using PDF default encoding
            return new PdfString(bytes.ToArray());
        }
    }
}