using System.Collections.Generic;
using System.IO;

namespace PdfReader.Fonts.PsFont
{
    internal class PsNumberConverter
    {
        public static void EncodeDictFloat(Stream s, float value)
        {
            // CFF DICT float encoding:30 + nibbles +0xF (terminator)
            s.WriteByte(30);

            string str = value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            List<byte> nibbles = new List<byte>();
            int i = 0;

            while (i < str.Length)
            {
                char c = str[i];
                if (c >= '0' && c <= '9')
                {
                    nibbles.Add((byte)(c - '0'));
                    i++;
                }
                else if (c == '.')
                {
                    nibbles.Add(0xA);
                    i++;
                }
                else if (c == 'E')
                {
                    if (str[i + 1] == '-')
                    {
                        nibbles.Add(0xC); // E-
                        i += 2; // skip E and -
                    }
                    else if (str[i + 1] == '+')
                    {
                        nibbles.Add(0xB); // E
                        i += 2; // skip E and +
                    }
                }
                else if (c == '-')
                {
                    nibbles.Add(0xE); // minus
                    i++;
                }
                else
                {
                    // Ignore any other character (should not occur)
                    i++;
                }
            }

            nibbles.Add(0xF); // terminator

            for (int j = 0; j < nibbles.Count; j += 2)
            {
                byte b;
                if (j + 1 < nibbles.Count)
                {
                    b = (byte)((nibbles[j] << 4) | nibbles[j + 1]);
                }
                else
                {
                    b = (byte)((nibbles[j] << 4) | 0xF);
                }

                s.WriteByte(b);
            }
        }

        public static void EncodeDictInteger(Stream s, int value)
        {
            // Correct CFF DICT integer encoding (no255 marker; use28 or29 for large values).
            if (value >= -107 && value <= 107)
            {
                s.WriteByte((byte)(value + 139));
                return;
            }
            if (value >= 108 && value <= 1131)
            {
                int v = value - 108;
                int b1 = v / 256;
                int b2 = v % 256;
                s.WriteByte((byte)(247 + b1));
                s.WriteByte((byte)b2);
                return;
            }
            if (value >= -1131 && value <= -108)
            {
                int v = -value - 108;
                int b1 = v / 256;
                int b2 = v % 256;
                s.WriteByte((byte)(251 + b1));
                s.WriteByte((byte)b2);
                return;
            }
            if (value >= -32768 && value <= 32767)
            {
                //16-bit integer encoding:28 + big-endian int16
                s.WriteByte(28);
                unchecked
                {
                    s.WriteByte((byte)((value >> 8) & 0xFF));
                    s.WriteByte((byte)(value & 0xFF));
                }
                return;
            }
            //32-bit integer encoding:29 + big-endian int32
            s.WriteByte(29);
            unchecked
            {
                s.WriteByte((byte)((value >> 24) & 0xFF));
                s.WriteByte((byte)((value >> 16) & 0xFF));
                s.WriteByte((byte)((value >> 8) & 0xFF));
                s.WriteByte((byte)(value & 0xFF));
            }
        }

        public static void EncodeCharStringNumber(Stream s, int value)
        {
            // 1-byte encoding: -107 .. 107
            if (value >= -107 && value <= 107)
            {
                s.WriteByte((byte)(value + 139));
                return;
            }

            // 2-byte positive: 108 .. 1131
            if (value >= 108 && value <= 1131)
            {
                int v = value - 108;
                byte b1 = (byte)(v / 256);
                byte b2 = (byte)(v % 256);
                s.WriteByte((byte)(247 + b1));
                s.WriteByte(b2);
                return;
            }

            // 2-byte negative: -1131 .. -108
            if (value >= -1131 && value <= -108)
            {
                int v = -value - 108;
                byte b1 = (byte)(v / 256);
                byte b2 = (byte)(v % 256);
                s.WriteByte((byte)(251 + b1));
                s.WriteByte(b2);
                return;
            }

            // ShortInt (Type2 only): -32768 .. 32767 excluding ranges already handled above.
            if (value >= -32768 && value <= 32767)
            {
                s.WriteByte(28); // shortint marker
                unchecked
                {
                    s.WriteByte((byte)((value >> 8) & 0xFF));
                    s.WriteByte((byte)(value & 0xFF));
                }
                return;
            }

            // LongInt (Type2 only): 255 + 4 bytes big-endian two's complement
            s.WriteByte(255);
            unchecked
            {
                s.WriteByte((byte)((value >> 24) & 0xFF));
                s.WriteByte((byte)((value >> 16) & 0xFF));
                s.WriteByte((byte)((value >> 8) & 0xFF));
                s.WriteByte((byte)(value & 0xFF));
            }
        }
    }
}
