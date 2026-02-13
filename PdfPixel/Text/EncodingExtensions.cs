using System;
using System.Text;
using System.Text.RegularExpressions;
using PdfPixel.Models;

namespace PdfPixel.Text;

public static class EncodingExtensions
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

    /// <summary>
    /// Decodes a <see cref="PdfPixel.Models.PdfString"/> into a .NET string using PDF string rules.
    /// Detects UTF-16BE/UTF-16LE/UTF-8 BOMs and decodes accordingly; otherwise falls back to PDF default (ISO-8859-1).
    /// When <paramref name="keepEscapeSequence"/> is false, removes language escape sequences bracketed by 0x1B.
    /// </summary>
    /// <param name="value">The PDF string to decode.</param>
    /// <param name="keepEscapeSequence">If true, do not remove 0x1B escape sequences.</param>
    /// <returns>Decoded string.</returns>
    public static string DecodePdfString(this PdfString value, bool keepEscapeSequence = false)
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
            Encoding encoding = null;

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
                catch
                {
                    // Fall back to ISO-8859-1 below on any decoding error
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

        return encoding.GetString(value.Span);
    }
}

/*
 function stringToPDFString(str, keepEscapeSequence = false) {
// See section 7.9.2.2 Text String Type.
// The string can contain some language codes bracketed with 0x1b,
// so we must remove them.
if (str[0] >= "\xEF") {
let encoding;
if (str[0] === "\xFE" && str[1] === "\xFF") {
  encoding = "utf-16be";
  if (str.length % 2 === 1) {
    str = str.slice(0, -1);
  }
} else if (str[0] === "\xFF" && str[1] === "\xFE") {
  encoding = "utf-16le";
  if (str.length % 2 === 1) {
    str = str.slice(0, -1);
  }
} else if (str[0] === "\xEF" && str[1] === "\xBB" && str[2] === "\xBF") {
  encoding = "utf-8";
}

if (encoding) {
  try {
    const decoder = new TextDecoder(encoding, { fatal: true });
    const buffer = stringToBytes(str);
    const decoded = decoder.decode(buffer);
    if (keepEscapeSequence || !decoded.includes("\x1b")) {
      return decoded;
    }
    return decoded.replaceAll(/\x1b[^\x1b]*(?:\x1b|$)/g, "");
  } catch (ex) {
    warn(`stringToPDFString: "${ex}".`);
  }
}
}
// ISO Latin 1
const strBuf = [];
for (let i = 0, ii = str.length; i < ii; i++) {
const charCode = str.charCodeAt(i);
if (!keepEscapeSequence && charCode === 0x1b) {
  // eslint-disable-next-line no-empty
  while (++i < ii && str.charCodeAt(i) !== 0x1b) {}
  continue;
}
const code = PDFStringTranslateTable[charCode];
strBuf.push(code ? String.fromCharCode(code) : str.charAt(i));
}
return strBuf.join("");
}
 */
