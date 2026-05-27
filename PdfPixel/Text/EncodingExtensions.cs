using System;
using System.Text;
using System.Text.RegularExpressions;
using PdfPixel.Models;

namespace PdfPixel.Text;

public static class EncodingExtensions
{
    public static readonly Encoding PdfDefault = Encoding.GetEncoding("ISO-8859-1");

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
    /// Decodes a <see cref="PdfPixel.Models.PdfString"/> into a .NET string using PDF string rules.
    /// Detects UTF-16BE/UTF-16LE/UTF-8 BOMs and decodes accordingly; otherwise falls back to PDF default (ISO-8859-1).
    /// When <paramref name="keepEscapeSequence"/> is false, removes language escape sequences bracketed by 0x1B.
    /// </summary>
    /// <param name="value">The PDF string to decode.</param>
    /// <param name="keepEscapeSequence">If true, do not remove 0x1B escape sequences.</param>
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

        // If first byte suggests a BOM/UTF indicator, try BOM-based decoding first
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

        // Fallback: ISO Latin-1 (PDF default)
        string result = PdfDefault.GetString(value.Value);
        return CleanupEscapeSequence(result, keepEscapeSequence);
    }

    private static string CleanupEscapeSequence(string result, bool keepEscapeSequence)
    {
        if (keepEscapeSequence || !result.Contains("\x1b"))
        {
            return result;
        }

        return Regex.Replace(result, @"\x1b[^\x1b]*(?:\x1b|$)", string.Empty);
    }

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
