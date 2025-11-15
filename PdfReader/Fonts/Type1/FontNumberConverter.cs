using System.Collections.Generic;
using System.IO;

namespace PdfReader.Fonts.Type1;

/// <summary>
/// Provides methods for encoding numbers in CFF/Type1 font dictionaries and charstrings.
/// </summary>
internal class FontNumberConverter
{
    /// <summary>
    /// Encodes a floating-point value using CFF DICT float encoding and writes it to the stream.
    /// </summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="value">The float value to encode.</param>
    public static void EncodeDictFloat(Stream stream, float value)
    {
        // CFF DICT float encoding:30 + nibbles +0xF (terminator)
        stream.WriteByte(30);

        string floatString = value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        List<byte> nibbles = new List<byte>();
        int index = 0;

        while (index < floatString.Length)
        {
            char character = floatString[index];
            if (character >= '0' && character <= '9')
            {
                nibbles.Add((byte)(character - '0'));
                index++;
            }
            else if (character == '.')
            {
                nibbles.Add(0xA);
                index++;
            }
            else if (character == 'E')
            {
                if (index + 1 < floatString.Length)
                {
                    if (floatString[index + 1] == '-')
                    {
                        nibbles.Add(0xC); // E-
                        index += 2;
                    }
                    else if (floatString[index + 1] == '+')
                    {
                        nibbles.Add(0xB); // E+
                        index += 2;
                    }
                    else
                    {
                        // Ignore unexpected character after E
                        index++;
                    }
                }
                else
                {
                    // Ignore trailing E
                    index++;
                }
            }
            else if (character == '-')
            {
                nibbles.Add(0xE); // minus
                index++;
            }
            else
            {
                // Ignore any other character (should not occur)
                index++;
            }
        }

        nibbles.Add(0xF); // terminator

        for (int nibbleIndex = 0; nibbleIndex < nibbles.Count; nibbleIndex += 2)
        {
            byte encodedByte;
            if (nibbleIndex + 1 < nibbles.Count)
            {
                encodedByte = (byte)((nibbles[nibbleIndex] << 4) | nibbles[nibbleIndex + 1]);
            }
            else
            {
                encodedByte = (byte)((nibbles[nibbleIndex] << 4) | 0xF);
            }

            stream.WriteByte(encodedByte);
        }
    }

    /// <summary>
    /// Encodes an integer value using CFF DICT integer encoding and writes it to the stream.
    /// </summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="value">The integer value to encode.</param>
    public static void EncodeDictInteger(Stream stream, int value)
    {
        // CFF DICT integer encoding (no255 marker; use28 or29 for large values).
        if (value >= -107 && value <= 107)
        {
            stream.WriteByte((byte)(value + 139));
            return;
        }
        if (value >= 108 && value <= 1131)
        {
            int adjustedValue = value - 108;
            int highByte = adjustedValue / 256;
            int lowByte = adjustedValue % 256;
            stream.WriteByte((byte)(247 + highByte));
            stream.WriteByte((byte)lowByte);
            return;
        }
        if (value >= -1131 && value <= -108)
        {
            int adjustedValue = -value - 108;
            int highByte = adjustedValue / 256;
            int lowByte = adjustedValue % 256;
            stream.WriteByte((byte)(251 + highByte));
            stream.WriteByte((byte)lowByte);
            return;
        }
        if (value >= -32768 && value <= 32767)
        {
            //16-bit integer encoding:28 + big-endian int16
            stream.WriteByte(28);
            unchecked
            {
                stream.WriteByte((byte)((value >> 8) & 0xFF));
                stream.WriteByte((byte)(value & 0xFF));
            }
            return;
        }
        //32-bit integer encoding:29 + big-endian int32
        stream.WriteByte(29);
        unchecked
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }
    }

    /// <summary>
    /// Encodes an integer value for Type1/Type2 charstrings and writes it to the stream.
    /// </summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="value">The integer value to encode.</param>
    public static void EncodeCharStringNumber(Stream stream, int value)
    {
        //1-byte encoding: -107 ..107
        if (value >= -107 && value <= 107)
        {
            stream.WriteByte((byte)(value + 139));
            return;
        }

        //2-byte positive:108 ..1131
        if (value >= 108 && value <= 1131)
        {
            int adjustedValue = value - 108;
            byte highByte = (byte)(adjustedValue / 256);
            byte lowByte = (byte)(adjustedValue % 256);
            stream.WriteByte((byte)(247 + highByte));
            stream.WriteByte(lowByte);
            return;
        }

        //2-byte negative: -1131 .. -108
        if (value >= -1131 && value <= -108)
        {
            int adjustedValue = -value - 108;
            byte highByte = (byte)(adjustedValue / 256);
            byte lowByte = (byte)(adjustedValue % 256);
            stream.WriteByte((byte)(251 + highByte));
            stream.WriteByte(lowByte);
            return;
        }

        // ShortInt (Type2 only): -32768 ..32767 excluding ranges already handled above.
        if (value >= -32768 && value <= 32767)
        {
            stream.WriteByte(28); // shortint marker
            unchecked
            {
                stream.WriteByte((byte)((value >> 8) & 0xFF));
                stream.WriteByte((byte)(value & 0xFF));
            }
            return;
        }

        // LongInt (Type2 only):255 +4 bytes big-endian two's complement
        stream.WriteByte(255);
        unchecked
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }
    }
}
