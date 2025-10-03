using System;
using System.Text;

namespace PdfReader.Fonts
{
    internal static class EncodingExtensions
    {
        public static Encoding PdfDefault = Encoding.GetEncoding("ISO-8859-1");

        public static string GetString(this Encoding encoding, ReadOnlySpan<byte> value)
        {
            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            if (value.Length == 0)
            {
                return string.Empty;
            }

            unsafe
            {
                fixed (byte* pBytes = value)
                {
                    return encoding.GetString(pBytes, value.Length);
                }
            }
        }

        public static string GetString(this Encoding encoding, ReadOnlyMemory<byte> value)
        {
            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            if (value.Length == 0)
            {
                return string.Empty;
            }

            return GetString(encoding, value.Span);
        }
    }
}
