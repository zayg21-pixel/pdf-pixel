using System;
using System.Text;

namespace PdfPixel.Objects;

/// <summary>
/// Extension and helper methods for text encoding used in PDF processing.
/// </summary>
public static class PdfEncodingExtensions
{
    private const int ReplacementCharacter = 0xFFFD;

    /// <summary>
    /// The PDF default string encoding (ISO-8859-1 / Latin-1).
    /// </summary>
    public static readonly Encoding PdfDefault = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// Decodes a span of bytes to a string using the specified encoding.
    /// </summary>
    public static string GetString(this Encoding encoding, in ReadOnlySpan<byte> value)
    {
        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        if (value.Length == 0)
        {
            return string.Empty;
        }

#if NETSTANDARD2_0
        return encoding.GetString(value.ToArray());
#else
        return encoding.GetString(value);
#endif
    }

    /// <summary>
    /// Decodes a <see cref="PdfString"/> into a .NET string using PDF string rules.
    /// Detects UTF-16BE/UTF-16LE/UTF-8 BOMs and decodes accordingly; otherwise falls back to <see cref="PdfDefault"/>.
    /// </summary>
    /// <param name="value">The PDF string to decode.</param>
    /// <param name="keepEscapeSequence">If true, keeps the language escape sequences bracketed by 0x1B.</param>
    /// <returns>Decoded string.</returns>
    public static string DecodePdfString(this in PdfString value, bool keepEscapeSequence = false)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        ReadOnlySpan<byte> span = value.Value.Span;

        if (span.IsEmpty)
        {
            return string.Empty;
        }

        if (span[0] >= 0xEF)
        {
            Encoding? encoding = null;

            if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode; // UTF-16BE
                if ((span.Length % 2) == 1)
                {
                    span = span.Slice(0, span.Length - 1);
                }
            }
            else if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
            {
                encoding = Encoding.Unicode; // UTF-16LE
                if ((span.Length % 2) == 1)
                {
                    span = span.Slice(0, span.Length - 1);
                }
            }
            else if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            {
                encoding = Encoding.UTF8;
            }

            if (encoding != null)
            {
                try
                {
                    string decoded = encoding.GetString(span);
                    return CleanupEscapeSequence(decoded, keepEscapeSequence);
                }
                catch (DecoderFallbackException)
                {
                    // Fall back to ISO-8859-1 below
                }
            }
        }

        string result = PdfDefault.GetString(value.Value);
        return CleanupEscapeSequence(result, keepEscapeSequence);
    }

    /// <summary>
    /// Returns the Unicode code point starting at <paramref name="index"/>; an unpaired surrogate reads as U+FFFD.
    /// </summary>
    /// <param name="text">The text to read from.</param>
    /// <param name="index">The index of the code point's first UTF-16 code unit.</param>
    /// <param name="length">The number of UTF-16 code units the code point occupies.</param>
    public static int GetCodePoint(this string text, int index, out int length)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (char.IsSurrogatePair(text, index))
        {
            length = 2;
            return char.ConvertToUtf32(text[index], text[index + 1]);
        }

        length = 1;
        if (char.IsSurrogate(text[index]))
        {
            return ReplacementCharacter;
        }

        return text[index];
    }

    private static string CleanupEscapeSequence(string result, bool keepEscapeSequence)
    {
        if (keepEscapeSequence || !result.Contains("\x1b"))
        {
            return result;
        }

        StringBuilder builder = new(result.Length);
        int index = 0;

        while (index < result.Length)
        {
            int escapeStart = result.IndexOf('\x1b', index);

            if (escapeStart < 0)
            {
                builder.Append(result, index, result.Length - index);
                break;
            }

            builder.Append(result, index, escapeStart - index);
            int escapeEnd = result.IndexOf('\x1b', escapeStart + 1);

            if (escapeEnd < 0)
            {
                break;
            }

            index = escapeEnd + 1;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Decodes a <c>ReadOnlyMemory&lt;byte&gt;</c> to a string using the specified encoding.
    /// </summary>
    public static string GetString(this Encoding encoding, in ReadOnlyMemory<byte> value)
    {
        if (encoding == null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        if (value.Length == 0)
        {
            return string.Empty;
        }

        return encoding.GetString(value.Span);
    }
}
