using PdfPixel.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

partial struct PdfParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue ReadHexString()
    {
        // Initial '<' already consumed by ReadToken.
        // Single-pass implementation using reusable _charBuffer (mirrors ReadString style).
        _localBuffer.Clear();
        int? highNibble = null;
        while (!IsAtEnd)
        {
            byte currentByte = ReadByte();

            if (currentByte == RightAngle)
            {
                break;
            }
            else if (IsWhitespace(currentByte))
            {
                continue;
            }
            else if (!IsHexDigit(currentByte))
            {
                // Skip invalid characters silently (same logical behavior as previous multi-pass version).
                continue;
            }

            int nibble = HexDigitToValue(currentByte);
            if (highNibble == null)
            {
                highNibble = nibble;
            }
            else
            {
                _localBuffer.Add((byte)((highNibble.Value << 4) | nibble));
                highNibble = null;
            }
        }
        if (highNibble != null)
        {
            // Odd number of hex digits: pad low nibble with0.
            _localBuffer.Add((byte)(highNibble.Value << 4));
        }

        ReadOnlyMemory<byte> bytes = _localBuffer.ToArray();

        if (_decrypt && _document.Decryptor != null && _currentReference.IsValid)
        {
            bytes = _document.Decryptor.DecryptString(bytes, _currentReference);
        }

        return PdfValueFactory.String(new PdfString(bytes));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue ReadString()
    {
        // Initial '(' already consumed by ReadToken.
        // Single-pass variant using _charBuffer list to accumulate bytes.
        _localBuffer.Clear();
        int parenCount = 1;
        while (!IsAtEnd && parenCount > 0)
        {
            byte readByte = ReadByte();
            switch (readByte)
            {
                case LeftParen:
                    parenCount++;
                    if (parenCount > 1)
                    {
                        _localBuffer.Add(readByte);
                    }
                    break;
                case RightParen:
                    parenCount--;
                    if (parenCount > 0)
                    {
                        _localBuffer.Add(readByte);
                    }
                    break;
                case Backslash:
                {
                    if (!IsAtEnd)
                    {
                        byte nextByte = ReadByte();
                        switch (nextByte)
                        {
                            case LineFeed:
                                continue; // Skip LF
                            case CarriageReturn:
                                if (!IsAtEnd && PeekByte() == LineFeed)
                                {
                                    Advance(1); // Skip optional LF in CRLF
                                }
                                continue; // Skip CR
                            case >= (byte)'0' and <= (byte)'7':
                            {
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
                                _localBuffer.Add((byte)(octalValue & 0xFF));
                                break;
                            }
                            default:
                            {
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
                                _localBuffer.Add(mappedByte);
                                break;
                            }
                        }
                    }
                    break;
                }
                default:
                    _localBuffer.Add(readByte);
                    break;
            }
        }

        ReadOnlyMemory<byte> bytes = _localBuffer.ToArray();

        if (_decrypt && _document.Decryptor != null && _currentReference.IsValid)
        {
            bytes = _document.Decryptor.DecryptString(bytes, _currentReference);
        }

        return PdfValueFactory.String(new PdfString(bytes));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue ReadName()
    {
        // Initial '/' already consumed by ReadToken.
        _localBuffer.Clear();

        while (!IsAtEnd)
        {
            byte currentByte = PeekByte();
            if (IsTokenTerminator(currentByte))
            {
                break;
            }
            Advance(1);
            if (currentByte == (byte)'#')
            {
                // Read two hex digits and convert to a single byte.
                byte hex1 = ReadByte();
                byte hex2 = ReadByte();
                int hexValue = (HexDigitToValue(hex1) << 4) | HexDigitToValue(hex2);
                _localBuffer.Add((byte)hexValue);
            }
            else
            {
                _localBuffer.Add(currentByte);
            }
        }
        return PdfValueFactory.Name(new PdfString([.. _localBuffer]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue ReadOperator()
    {
        // Single-pass operator parsing using _charBuffer (avoids scan + slice allocation).
        _localBuffer.Clear();
        while (!IsAtEnd)
        {
            byte currentByte = PeekByte();
            if (IsTokenTerminator(currentByte))
            {
                break;
            }
            _localBuffer.Add(ReadByte());
        }

        ReadOnlyMemory<byte> result = _localBuffer.ToArray();

        if (result.Span.SequenceEqual(TrueValue))
        {
            return PdfValueFactory.Boolean(true);
        }
        else if (result.Span.SequenceEqual(FalseValue))
        {
            return PdfValueFactory.Boolean(false);
        }
        else if (result.Span.SequenceEqual(NullValue))
        {
            return PdfValueFactory.Null();
        }
        else
        {
            return PdfValueFactory.Operator(result);
        }
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
