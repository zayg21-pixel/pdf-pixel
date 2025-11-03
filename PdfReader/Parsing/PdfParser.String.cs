using PdfReader.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    partial struct PdfParser
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IPdfValue ReadHexString()
        {
            // Initial '<' already consumed by ReadToken.
            
            // Stage 1: Estimate number of hex digits and validate content
            int hexDigitCount = 0;
            int scanOffset = 0;
            bool hasWhitespaces = false;
            
            // Scan ahead to count hex digits and detect invalid characters
            while (Position + scanOffset < Length)
            {
                byte currentByte = PeekByte(scanOffset);
                
                if (currentByte == RightAngle)
                {
                    break;
                }
                
                // Check if it's whitespace (skip counting)
                if (IsWhitespace(currentByte))
                {
                    hasWhitespaces = true;
                    scanOffset++;
                    continue;
                }
                
                // Check if it's a valid hex digit using helper
                if (IsHexDigit(currentByte))
                {
                    hexDigitCount++;
                    scanOffset++;
                }
                else
                {
                    // Invalid character found - continue scanning (will be skipped in stage 3)
                    scanOffset++;
                }
            }
            
            // Stage 2: Create array with exact required capacity
            // Each pair of hex digits creates one byte, odd count gets padded
            int expectedByteCount = (hexDigitCount + 1) / 2;
            byte[] bytes = new byte[expectedByteCount];
            int byteIndex = 0;
            
            // Stage 3: Fill array with values, skipping invalid characters
            int? highNibble = null;

            while (!IsAtEnd)
            {
                // Read and process the byte
                byte readByte = ReadByte();

                if (readByte == RightAngle)
                {
                    break;
                }

                // Skip whitespace efficiently
                if (hasWhitespaces && IsWhitespace(readByte))
                {
                    continue;
                }

                // Process hex digit or skip invalid characters using helper
                if (IsHexDigit(readByte))
                {
                    int nibble = HexDigitToValue(readByte);
                    
                    if (highNibble == null)
                    {
                        highNibble = nibble;
                    }
                    else
                    {
                        bytes[byteIndex++] = (byte)((highNibble.Value << 4) | nibble);
                        highNibble = null;
                    }
                }
                // If not a hex digit, skip invalid character
            }

            // Handle odd number of hex digits (pad with zero)
            if (highNibble != null)
            {
                bytes[byteIndex++] = (byte)(highNibble.Value << 4);
            }

            // Create PdfString directly from the byte array
            var pdfString = new PdfString(bytes);
            return PdfValue.String(pdfString);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IPdfValue ReadString()
        {
            // Initial '(' already consumed by ReadToken.
            
            // Stage 1: Scan ahead to estimate final string length
            int finalLength = 0;
            int scanOffset = 0;
            int parenCount = 1; // We've already consumed the opening '('
            bool hasEscapes = false;
            
            while (Position + scanOffset < Length && parenCount > 0)
            {
                byte currentByte = PeekByte(scanOffset);
                
                if (currentByte == LeftParen)
                {
                    parenCount++;
                    finalLength++;
                    scanOffset++;
                }
                else if (currentByte == RightParen)
                {
                    parenCount--;
                    if (parenCount > 0)
                    {
                        finalLength++;
                    }
                    scanOffset++;
                }
                else if (currentByte == Backslash)
                {
                    hasEscapes = true;
                    scanOffset++; // Skip backslash
                    if (Position + scanOffset < Length)
                    {
                        byte nextByte = PeekByte(scanOffset);
                        scanOffset++;
                        
                        // Line continuation (CR, LF, CRLF) -> contributes 0 to final length
                        if (nextByte == LineFeed)
                        {
                            // LF - no contribution to final length
                        }
                        else if (nextByte == CarriageReturn)
                        {
                            // CR - check for optional LF
                            if (Position + scanOffset < Length && PeekByte(scanOffset) == LineFeed)
                            {
                                scanOffset++; // Skip LF in CRLF
                            }
                            // No contribution to final length
                        }
                        else if (nextByte >= (byte)'0' && nextByte <= (byte)'7')
                        {
                            // Octal escape - up to 3 digits, contributes 1 byte to final length
                            finalLength++;
                            // Skip additional octal digits (up to 2 more)
                            for (int i = 0; i < 2 && Position + scanOffset < Length; i++)
                            {
                                byte octalByte = PeekByte(scanOffset);
                                if (octalByte >= (byte)'0' && octalByte <= (byte)'7')
                                {
                                    scanOffset++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Other escape sequences (\n, \r, \t, \b, \f, \(, \), \\, etc.) -> contribute 1 byte
                            finalLength++;
                        }
                    }
                }
                else
                {
                    finalLength++;
                    scanOffset++;
                }
            }
            
            // Stage 2: Optimize for common case (no escape sequences)
            if (!hasEscapes)
            {
                // Fast path: use GetSlice directly for strings without escapes
                // scanOffset points past the closing ')', so we need scanOffset-1 for content length
                // and advance by full scanOffset to consume the closing ')'
                var stringSlice = ReadSliceFromCurrent(scanOffset - 1); // Exclude closing ')'
                Advance(1); // Consume everything including closing ')'
                return PdfValue.String(new PdfString(stringSlice));
            }
            
            // Stage 3: Handle strings with escape sequences - create exact array
            byte[] stringBytes = new byte[finalLength];
            int byteIndex = 0;
            parenCount = 1;
            
            while (!IsAtEnd && parenCount > 0)
            {
                byte readByte = ReadByte();
                
                if (readByte == LeftParen)
                {
                    parenCount++;
                    if (parenCount > 1) // Don't include the opening '('
                    {
                        stringBytes[byteIndex++] = readByte;
                    }
                }
                else if (readByte == RightParen)
                {
                    parenCount--;
                    if (parenCount > 0) // Don't include the final closing ')'
                    {
                        stringBytes[byteIndex++] = readByte;
                    }
                }
                else if (readByte == Backslash)
                {
                    // Handle escape sequence
                    if (!IsAtEnd)
                    {
                        byte nextByte = ReadByte();
                        
                        // Line continuation
                        if (nextByte == LineFeed)
                        {
                            // Skip LF - no output
                            continue;
                        }
                        else if (nextByte == CarriageReturn)
                        {
                            // Skip CR and optional LF - no output
                            if (!IsAtEnd && PeekByte() == LineFeed)
                            {
                                Advance(1);
                            }
                            continue;
                        }
                        else if (nextByte >= (byte)'0' && nextByte <= (byte)'7')
                        {
                            // Octal escape sequence
                            int octalValue = nextByte - (byte)'0';
                            for (int i = 0; i < 2 && !IsAtEnd; i++)
                            {
                                byte octalByte = PeekByte();
                                if (octalByte >= (byte)'0' && octalByte <= (byte)'7')
                                {
                                    Advance(1);
                                    octalValue = (octalValue << 3) + (octalByte - (byte)'0');
                                }
                                else
                                {
                                    break;
                                }
                            }
                            stringBytes[byteIndex++] = (byte)(octalValue & 0xFF);
                        }
                        else
                        {
                            // Standard escape sequences
                            byte mappedByte = nextByte switch
                            {
                                (byte)'n' => LineFeed,
                                (byte)'r' => CarriageReturn,
                                (byte)'t' => Tab,
                                (byte)'b' => (byte)'\b',
                                (byte)'f' => FormFeed,
                                (byte)'(' => LeftParen,
                                (byte)')' => RightParen,
                                (byte)'\\' => Backslash,
                                _ => nextByte
                            };
                            stringBytes[byteIndex++] = mappedByte;
                        }
                    }
                }
                else
                {
                    stringBytes[byteIndex++] = readByte;
                }
            }
            
            return PdfValue.String(new PdfString(stringBytes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IPdfValue ReadName()
        {
            // Initial '/' already consumed by ReadToken.
            
            // Stage 1: Scan ahead to find name end and count escape sequences
            int nameLength = 0;
            int escapeCount = 0;
            int scanOffset = 0;
            
            while (Position + scanOffset < Length)
            {
                byte currentByte = PeekByte(scanOffset);
                
                // Check for token terminators
                if (IsTokenTerminator(currentByte))
                {
                    break;
                }
                
                if (currentByte == (byte)'#')
                {
                    // Escape sequence #XX contributes 1 byte to final name length
                    escapeCount++;
                    nameLength++;
                    scanOffset += 3; // Skip #XX (3 bytes total)
                }
                else
                {
                    nameLength++;
                    scanOffset++;
                }
            }
            
            // Stage 2: Optimize for the common case (no escape sequences - 99.9%)
            if (escapeCount == 0)
            {
                // Fast path: use GetSlice directly for names without escapes
                var nameSlice = ReadSliceFromCurrent(scanOffset);
                var nameString = new PdfString(nameSlice);
                return PdfValue.Name(nameString);
            }
            
            // Stage 3: Handle names with escape sequences - create exact array
            byte[] nameBytes = new byte[nameLength];
            int byteIndex = 0;
            
            while (!IsAtEnd && byteIndex < nameLength)
            {
                byte currentByte = PeekByte();
                
                // Check for token terminators
                if (IsTokenTerminator(currentByte))
                {
                    break;
                }

                Advance(1);

                if (currentByte == (byte)'#')
                {
                    // Handle escape sequence #XX - just read 2 bytes and convert
                    byte hex1 = ReadByte();
                    byte hex2 = ReadByte();

                    int hexValue = (HexDigitToValue(hex1) << 4) | HexDigitToValue(hex2);
                    nameBytes[byteIndex++] = (byte)hexValue;
                }
                else
                {
                    // Regular character
                    nameBytes[byteIndex++] = currentByte;
                }
            }
            
            // Create PdfString directly from the byte array
            return PdfValue.Name(new PdfString(nameBytes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IPdfValue ReadOperator()
        {
            // Scan ahead to find operator end
            int scanOffset = 0;
            
            while (Position + scanOffset < Length)
            {
                byte currentByte = PeekByte(scanOffset);
                
                // Check for token terminators (same as names)
                if (IsTokenTerminator(currentByte))
                {
                    break;
                }
                
                scanOffset++;
            }
            
            // Use GetSlice directly since operators never have escape sequences
            var operatorSlice = ReadSliceFromCurrent(scanOffset);
            
            // Special case: check for boolean literals using SequenceEqual
            if (operatorSlice.Span.SequenceEqual(TrueValue))
            {
                return PdfValue.Boolean(true);
            }
            else if (operatorSlice.Span.SequenceEqual(FalseValue))
            {
                return PdfValue.Boolean(false);
            }
            
            // Regular operator
            return PdfValue.Operator(new PdfString(operatorSlice));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHexDigit(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9') ||
                   (b >= (byte)'A' && b <= (byte)'F') ||
                   (b >= (byte)'a' && b <= (byte)'f');
        }
        
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
                return 0;
            }
        }
    }
}
