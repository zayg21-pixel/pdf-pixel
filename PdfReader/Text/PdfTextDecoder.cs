using PdfReader.Fonts;
using System;
using System.Text;

namespace PdfReader.Text
{
    /// <summary>
    /// Font-aware text decoder for PDF content.
    /// Uses ToUnicode CMap when available; otherwise falls back to font encodings.
    /// Character-code handling is length-aware via PdfCharacterCode and codespace ranges (only for Type0/CID fonts).
    /// </summary>
    public static class PdfTextDecoder
    {
        // Register code pages (e.g., Windows-1252) for .NET Standard 2.0
        static PdfTextDecoder()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // Safe to ignore: if registration fails, only built-in encodings are available
            }
        }

        /// <summary>
        /// Decode a PDF string (raw bytes) to Unicode using the provided font.
        /// Priority:
        /// 1) ToUnicode CMap
        /// 2) Font encoding
        /// 3) UTF-8 fallback
        /// </summary>
        public static string DecodeTextStringWithFont(ReadOnlyMemory<byte> bytes, PdfFontBase font)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            if (font == null)
            {
                Console.WriteLine("Warning: Font is null, using UTF-8 fallback");
                return Encoding.UTF8.GetString(bytes);
            }

            // Priority 1: ToUnicode CMap
            if (font.ToUnicodeCMap != null)
            {
                return DecodeWithToUnicodeCMap(bytes, font);
            }

            // Priority 2: font encoding
            try
            {
                var encoding = GetEncodingForFont(font);
                if (encoding != null)
                {
                    return encoding.GetString(bytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to decode using font encoding for {font.BaseFont}: {ex.Message}");
            }

            // TODO: need fall-backs for CFF fonts without CMap

            // Priority 3: UTF-8 fallback
            Console.WriteLine($"Warning: Using UTF-8 fallback for font {font.BaseFont} (Type: {font.Type})");
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Decode a single byte-sequence character code to Unicode using ToUnicode or font encoding.
        /// </summary>
        public static string DecodeCharacterCode(PdfCharacterCode code, PdfFontBase font)
        {
            if (font == null)
            {
                Console.WriteLine("Warning: Font is null, using ISO-8859-1 fallback");
                return EncodingExtensions.PdfDefault.GetString(code.Bytes);
            }

            // Priority 1: ToUnicode CMap
            if (font.ToUnicodeCMap != null)
            {
                var unicode = font.ToUnicodeCMap.GetUnicode(code);
                if (unicode != null)
                {
                    return unicode;
                }

                Console.WriteLine($"Warning: Character code {code} not found in ToUnicode CMap for font {font.BaseFont}");
            }

            // Priority 2: font encoding as raw byte decode
            var enc = GetEncodingForFont(font);
            if (enc != null)
            {
                try
                {
                    return enc.GetString(code.Bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to decode character code {code} using encoding for {font.BaseFont}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Warning: No encoding available for font {font.BaseFont} (Type: {font.Type})");
            }

            // TODO: need fall-backs for CFF fonts without CMap

            // Fallback
            return EncodingExtensions.PdfDefault.GetString(code.Bytes);
        }

        /// <summary>
        /// Decode using ToUnicode CMap. For Type0/CID fonts, segment using codespace ranges (longest-match).
        /// For simple/Type3 fonts, always segment as single bytes (or fixed fallback).
        /// </summary>
        private static string DecodeWithToUnicodeCMap(ReadOnlyMemory<byte> bytes, PdfFontBase font)
        {
            var cmap = font.ToUnicodeCMap;
            var sb = new StringBuilder(bytes.Length);

            if (ShouldUseCodespaceRanges(font, cmap))
            {
                // Variable-length segmentation using codespace ranges
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int len = cmap.GetMaxMatchingLength(bytes.Slice(offset).Span);
                    if (len == 0)
                    {
                        // No range matched: consume 1 byte to avoid stalling
                        len = 1;
                    }

                    var code = new PdfCharacterCode(bytes.Slice(offset, len));
                    var unicode = cmap.GetUnicode(code);
                    if (unicode != null)
                    {
                        sb.Append(unicode);
                    }
                    else
                    {
                        // Fallback: interpret raw bytes as Latin-1 to preserve data visually
                        sb.Append(EncodingExtensions.PdfDefault.GetString(bytes.Slice(offset, len)));
                    }

                    offset += len;
                }

                return sb.ToString();
            }

            // Fixed-length fallback (simple fonts or no codespace ranges)
            var codes = ExtractCharacterCodesFromBytes(bytes, font);
            foreach (var code in codes)
            {
                var unicode = cmap.GetUnicode(code);
                if (unicode != null)
                {
                    sb.Append(unicode);
                }
                else
                {
                    sb.Append(EncodingExtensions.PdfDefault.GetString(code.Bytes));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determine whether to use codespace ranges for segmentation.
        /// Only for composite (Type0) and CID fonts, and only when ranges are present.
        /// </summary>
        private static bool ShouldUseCodespaceRanges(PdfFontBase font, PdfToUnicodeCMap cmap)
        {
            if (font == null || cmap == null)
            {
                return false;
            }

            // Simple (Type1/TrueType) and Type3 fonts are single-byte per spec
            if (!(font is PdfCompositeFont) && !(font is PdfCIDFont))
            {
                return false;
            }

            return cmap.HasCodeSpaceRanges && cmap.MaxCodeLength > 0;
        }

        /// <summary>
        /// Extract length-aware character codes from bytes using codespace ranges when available; otherwise fixed-length fallback.
        /// </summary>
        public static PdfCharacterCode[] ExtractCharacterCodesFromBytes(ReadOnlyMemory<byte> bytes, PdfFontBase font)
        {
            if (bytes.IsEmpty)
            {
                return Array.Empty<PdfCharacterCode>();
            }

            if (ShouldUseCodespaceRanges(font, font?.ToUnicodeCMap))
            {
                // Variable-length segmentation
                var cmap = font.ToUnicodeCMap;
                var list = new System.Collections.Generic.List<PdfCharacterCode>();
                int offset = 0;

                while (offset < bytes.Length)
                {
                    int len = cmap.GetMaxMatchingLength(bytes.Slice(offset).Span);
                    if (len == 0)
                    {
                        len = 1;
                    }

                    list.Add(new PdfCharacterCode(bytes.Slice(offset, len)));
                    offset += len;
                }

                return list.ToArray();
            }

            // Fixed-size segmentation based on font type/encoding
            int codeLength = GetCharacterCodeLength(font);

            // If we expect 2 bytes but the length is odd, fall back to 1 byte to avoid overruns
            if (codeLength == 2 && bytes.Length % 2 != 0)
            {
                Console.WriteLine($"Warning: Odd byte count ({bytes.Length}) with 2-byte expectation, using 1-byte segmentation");
                codeLength = 1;
            }

            int count = bytes.Length / codeLength;
            var result = new PdfCharacterCode[count];

            for (int i = 0; i < count; i++)
            {
                int offset = i * codeLength;
                result[i] = new PdfCharacterCode(bytes.Slice(offset, codeLength));
            }

            return result;
        }

        /// <summary>
        /// Get an Encoding appropriate for the given font when ToUnicode is not available.
        /// Note: For CID/Type0 with Identity encodings, return null to avoid misleading decoding.
        /// </summary>
        public static Encoding GetEncodingForFont(PdfFontBase font)
        {
            if (font == null)
            {
                Console.WriteLine("Warning: Cannot determine encoding for null font");
                return null;
            }

            switch (font)
            {
                case PdfCompositeFont compositeFont:
                    return GetEncodingForCompositeFont(compositeFont);

                case PdfCIDFont cidFont:
                    return GetEncodingForCIDFont(cidFont);

                case PdfSimpleFont simpleFont:
                    return GetEncodingForSimpleFont(simpleFont);

                case PdfType3Font type3Font:
                    return GetEncodingForType3Font(type3Font);

                default:
                    return GetEncodingByEncodingType(font.Encoding, font);
            }
        }

        private static Encoding GetEncodingForCompositeFont(PdfCompositeFont compositeFont)
        {
            var enc = GetEncodingByEncodingType(compositeFont.Encoding, compositeFont);
            if (enc != null)
            {
                return enc;
            }

            return null;
        }

        private static Encoding GetEncodingForCIDFont(PdfCIDFont cidFont)
        {
            switch (cidFont.Encoding)
            {
                case PdfFontEncoding.IdentityH:
                case PdfFontEncoding.IdentityV:
                    Console.WriteLine($"Info: {cidFont.Encoding} for {cidFont.BaseFont}. No direct byte->Unicode decoding without ToUnicode.");
                    return null;

                default:
                    return GetEncodingByEncodingType(cidFont.Encoding, cidFont);
            }
        }

        private static Encoding GetEncodingForSimpleFont(PdfSimpleFont simpleFont)
        {
            return GetEncodingByEncodingType(simpleFont.Encoding, simpleFont);
        }

        private static Encoding GetEncodingForType3Font(PdfType3Font type3Font)
        {
            var encoding = GetEncodingByEncodingType(type3Font.Encoding, type3Font);
            if (encoding == null)
            {
                Console.WriteLine($"Warning: Type3 font {type3Font.BaseFont} has no usable encoding, using ISO-8859-1");
                return EncodingExtensions.PdfDefault;
            }

            return encoding;
        }

        private static Encoding GetEncodingByEncodingType(PdfFontEncoding encoding, PdfFontBase font)
        {
            switch (encoding)
            {
                case PdfFontEncoding.StandardEncoding:
                    return EncodingExtensions.PdfDefault;

                case PdfFontEncoding.MacRomanEncoding:
                    try
                    {
                        return Encoding.GetEncoding("macintosh");
                    }
                    catch
                    {
                        Console.WriteLine($"Warning: macintosh encoding not available for font {font.BaseFont}, using ISO-8859-1");
                        return EncodingExtensions.PdfDefault;
                    }

                case PdfFontEncoding.WinAnsiEncoding:
                    return Encoding.GetEncoding(1252);

                case PdfFontEncoding.MacExpertEncoding:
                    Console.WriteLine($"Warning: MacExpert encoding for font {font.BaseFont} not supported, using ISO-8859-1");
                    return EncodingExtensions.PdfDefault;

                case PdfFontEncoding.IdentityH:
                case PdfFontEncoding.IdentityV:
                    return null;

                case PdfFontEncoding.UniJIS_UTF16_H:
                case PdfFontEncoding.UniJIS_UTF16_V:
                case PdfFontEncoding.UniGB_UTF16_H:
                case PdfFontEncoding.UniGB_UTF16_V:
                case PdfFontEncoding.UniCNS_UTF16_H:
                case PdfFontEncoding.UniCNS_UTF16_V:
                case PdfFontEncoding.UniKS_UTF16_H:
                case PdfFontEncoding.UniKS_UTF16_V:
                    Console.WriteLine($"Info: {encoding} encountered for {font.BaseFont}. Using UTF-16BE as best-effort when ToUnicode is absent.");
                    return Encoding.BigEndianUnicode;

                case PdfFontEncoding.Custom:
                    Console.WriteLine($"Warning: Custom encoding for font {font.BaseFont} not fully supported, using ISO-8859-1");
                    if (!string.IsNullOrEmpty(font.CustomEncoding))
                    {
                        Console.WriteLine($"  Custom encoding name: {font.CustomEncoding}");
                    }
                    return EncodingExtensions.PdfDefault;

                case PdfFontEncoding.Unknown:
                default:
                    return EncodingExtensions.PdfDefault;
            }
        }

        // Fixed-length fallback helpers used only when no codespace ranges are present

        /// <summary>
        /// Determine character code length based on font type and encoding.
        /// </summary>
        private static int GetCharacterCodeLength(PdfFontBase font)
        {
            if (font == null)
            {
                Console.WriteLine("Warning: Font is null in GetCharacterCodeLength, assuming 1-byte codes");
                return 1;
            }

            switch (font)
            {
                case PdfCompositeFont compositeFont:
                    return GetCharacterCodeLengthForComposite(compositeFont);

                case PdfCIDFont cidFont:
                    return GetCharacterCodeLengthForCID(cidFont);

                case PdfSimpleFont simpleFont:
                    return GetCharacterCodeLengthForSimple(simpleFont);

                case PdfType3Font type3Font:
                    return GetCharacterCodeLengthForType3Font(type3Font);

                default:
                    Console.WriteLine($"Warning: Unknown font type {font.GetType().Name}, using encoding-based code length");
                    return GetCharacterCodeLengthByEncoding(font.Encoding, font);
            }
        }

        private static int GetCharacterCodeLengthForComposite(PdfCompositeFont compositeFont)
        {
            var byEncoding = GetCharacterCodeLengthByEncoding(compositeFont.Encoding, compositeFont);
            if (byEncoding != 1)
            {
                return byEncoding;
            }
            return 2;
        }

        private static int GetCharacterCodeLengthForCID(PdfCIDFont cidFont)
        {
            Console.WriteLine($"Warning: CID font {cidFont.BaseFont} accessed directly - should typically be accessed through composite font");
            return 2;
        }

        private static int GetCharacterCodeLengthForSimple(PdfSimpleFont simpleFont)
        {
            var byEncoding = GetCharacterCodeLengthByEncoding(simpleFont.Encoding, simpleFont);
            if (byEncoding == 2)
            {
                Console.WriteLine($"Warning: Simple font {simpleFont.BaseFont} has 2-byte encoding {simpleFont.Encoding}, but simple fonts should use 1-byte codes");
            }
            return 1;
        }

        private static int GetCharacterCodeLengthForType3Font(PdfType3Font type3Font)
        {
            return 1;
        }

        private static int GetCharacterCodeLengthByEncoding(PdfFontEncoding encoding, PdfFontBase font)
        {
            switch (encoding)
            {
                case PdfFontEncoding.StandardEncoding:
                case PdfFontEncoding.MacRomanEncoding:
                case PdfFontEncoding.WinAnsiEncoding:
                case PdfFontEncoding.MacExpertEncoding:
                    return 1;

                case PdfFontEncoding.IdentityH:
                case PdfFontEncoding.IdentityV:
                    return 2;

                case PdfFontEncoding.UniJIS_UTF16_H:
                case PdfFontEncoding.UniJIS_UTF16_V:
                case PdfFontEncoding.UniGB_UTF16_H:
                case PdfFontEncoding.UniGB_UTF16_V:
                case PdfFontEncoding.UniCNS_UTF16_H:
                case PdfFontEncoding.UniCNS_UTF16_V:
                case PdfFontEncoding.UniKS_UTF16_H:
                case PdfFontEncoding.UniKS_UTF16_V:
                    return 2;

                case PdfFontEncoding.Custom:
                    Console.WriteLine($"Warning: Custom encoding for font {font.BaseFont} - assuming 1-byte codes");
                    if (!string.IsNullOrEmpty(font.CustomEncoding))
                    {
                        Console.WriteLine($"  Custom encoding name: {font.CustomEncoding}");
                    }
                    return 1;

                case PdfFontEncoding.Unknown:
                default:
                    return 1;
            }
        }
    }
}