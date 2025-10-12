using PdfReader.Fonts;
using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.Types;
using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PdfReader.Text
{
    /// <summary>
    /// Font-aware text decoder for PDF content.
    /// Uses ToUnicode CMap when available; otherwise falls back to font encodings.
    /// Character-code handling is length-aware via PdfCharacterCode and codespace ranges (only for Type0/CID fonts).
    /// Instance-based to allow structured logging.
    /// </summary>
    public sealed class PdfTextDecoder
    {
        private readonly ILogger _logger;

        static PdfTextDecoder()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // Safe to ignore: if registration fails, only built-in encodings are available.
            }
        }

        /// <summary>
        /// Create a new decoder instance.
        /// </summary>
        public PdfTextDecoder(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PdfTextDecoder>();
        }

        /// <summary>
        /// Decode a PDF string (raw bytes) to Unicode using the provided font.
        /// Priority:
        /// 1) ToUnicode CMap
        /// 2) Font encoding
        /// 3) UTF-8 fallback
        /// </summary>
        public string DecodeTextStringWithFont(ReadOnlyMemory<byte> bytes, PdfFontBase font)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            if (font == null)
            {
                _logger.LogWarning("Font is null, using UTF-8 fallback for raw text of length {Length}.", bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }

            if (font.ToUnicodeCMap != null)
            {
                return DecodeWithToUnicodeCMap(bytes, font);
            }

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
                _logger.LogWarning(ex, "Failed to decode using font encoding for {Font}.", font.BaseFont);
            }

            // TODO: need fall-backs for CFF fonts without CMap
            _logger.LogWarning("Using UTF-8 fallback for font {Font} (Type: {Type}).", font.BaseFont, font.Type);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Decode a single byte-sequence character code to Unicode using ToUnicode or font encoding.
        /// </summary>
        public string DecodeCharacterCode(PdfCharacterCode code, PdfFontBase font)
        {
            if (font == null)
            {
                _logger.LogWarning("Font is null in DecodeCharacterCode, using ISO-8859-1 fallback.");
                return EncodingExtensions.PdfDefault.GetString(code.Bytes);
            }

            if (font.ToUnicodeCMap != null)
            {
                var unicode = font.ToUnicodeCMap.GetUnicode(code);
                if (unicode != null)
                {
                    return unicode;
                }
            }

            var enc = GetEncodingForFont(font);
            if (enc != null)
            {
                try
                {
                    return enc.GetString(code.Bytes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode character code {Code} using encoding for {Font}.", code, font.BaseFont);
                }
            }
            else
            {
                _logger.LogInformation("No encoding available for font {Font} (Type: {Type}).", font.BaseFont, font.Type);
            }

            // TODO: need fall-backs for CFF fonts without CMap
            return EncodingExtensions.PdfDefault.GetString(code.Bytes);
        }

        private string DecodeWithToUnicodeCMap(ReadOnlyMemory<byte> bytes, PdfFontBase font)
        {
            var cmap = font.ToUnicodeCMap;
            var sb = new StringBuilder(bytes.Length);

            if (ShouldUseCodespaceRanges(font, cmap))
            {
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int length = cmap.GetMaxMatchingLength(bytes.Slice(offset).Span);
                    if (length == 0)
                    {
                        length = 1; // consume one byte to avoid infinite loop
                    }

                    var code = new PdfCharacterCode(bytes.Slice(offset, length));
                    var unicode = cmap.GetUnicode(code);
                    if (unicode != null)
                    {
                        sb.Append(unicode);
                    }
                    else
                    {
                        sb.Append(EncodingExtensions.PdfDefault.GetString(bytes.Slice(offset, length)));
                    }
                    offset += length;
                }
                return sb.ToString();
            }

            var codes = ExtractCharacterCodes(bytes, font);
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

        private static bool ShouldUseCodespaceRanges(PdfFontBase font, PdfToUnicodeCMap cmap)
        {
            if (font == null || cmap == null)
            {
                return false;
            }
            if (!(font is PdfCompositeFont) && !(font is PdfCIDFont))
            {
                return false;
            }
            return cmap.HasCodeSpaceRanges && cmap.MaxCodeLength > 0;
        }

        public PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes, PdfFontBase font)
        {
            if (bytes.IsEmpty)
            {
                return Array.Empty<PdfCharacterCode>();
            }

            if (ShouldUseCodespaceRanges(font, font?.ToUnicodeCMap))
            {
                var cmap = font.ToUnicodeCMap;
                var list = new System.Collections.Generic.List<PdfCharacterCode>();
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int length = cmap.GetMaxMatchingLength(bytes.Slice(offset).Span);
                    if (length == 0)
                    {
                        length = 1;
                    }
                    list.Add(new PdfCharacterCode(bytes.Slice(offset, length)));
                    offset += length;
                }
                return list.ToArray();
            }

            int codeLength = GetCharacterCodeLength(font);
            if (codeLength == 2 && bytes.Length % 2 != 0)
            {
                _logger.LogInformation("Odd byte count {Length} with 2-byte expectation, using 1-byte segmentation.", bytes.Length);
                codeLength = 1;
            }

            int count = bytes.Length / codeLength;
            var result = new PdfCharacterCode[count];
            for (int index = 0; index < count; index++)
            {
                int offset = index * codeLength;
                result[index] = new PdfCharacterCode(bytes.Slice(offset, codeLength));
            }
            return result;
        }

        public Encoding GetEncodingForFont(PdfFontBase font)
        {
            if (font == null)
            {
                _logger.LogWarning("Cannot determine encoding for null font.");
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

        private Encoding GetEncodingForCompositeFont(PdfCompositeFont compositeFont)
        {
            var enc = GetEncodingByEncodingType(compositeFont.Encoding, compositeFont);
            return enc;
        }

        private Encoding GetEncodingForCIDFont(PdfCIDFont cidFont)
        {
            switch (cidFont.Encoding)
            {
                case PdfFontEncoding.IdentityH:
                case PdfFontEncoding.IdentityV:
                    _logger.LogInformation("{Encoding} for {Font}. No direct byte->Unicode decoding without ToUnicode.", cidFont.Encoding, cidFont.BaseFont);
                    return null;
                default:
                    return GetEncodingByEncodingType(cidFont.Encoding, cidFont);
            }
        }

        private Encoding GetEncodingForSimpleFont(PdfSimpleFont simpleFont)
        {
            return GetEncodingByEncodingType(simpleFont.Encoding, simpleFont);
        }

        private Encoding GetEncodingForType3Font(PdfType3Font type3Font)
        {
            var encoding = GetEncodingByEncodingType(type3Font.Encoding, type3Font);
            if (encoding == null)
            {
                _logger.LogWarning("Type3 font {Font} has no usable encoding, using ISO-8859-1.", type3Font.BaseFont);
                return EncodingExtensions.PdfDefault;
            }
            return encoding;
        }

        private Encoding GetEncodingByEncodingType(PdfFontEncoding encoding, PdfFontBase font)
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
                        _logger.LogWarning("macintosh encoding not available for font {Font}, using ISO-8859-1.", font.BaseFont);
                        return EncodingExtensions.PdfDefault;
                    }
                case PdfFontEncoding.WinAnsiEncoding:
                    return Encoding.GetEncoding(1252);
                case PdfFontEncoding.MacExpertEncoding:
                    _logger.LogWarning("MacExpert encoding for font {Font} not supported, using ISO-8859-1.", font.BaseFont);
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
                    _logger.LogInformation("{Encoding} encountered for {Font}. Using UTF-16BE as best-effort when ToUnicode is absent.", encoding, font.BaseFont);
                    return Encoding.BigEndianUnicode;
                case PdfFontEncoding.Custom:
                    _logger.LogWarning("Custom encoding for font {Font} not fully supported, using ISO-8859-1. Name={Custom}.", font.BaseFont, font.CustomEncoding);
                    return EncodingExtensions.PdfDefault;
                case PdfFontEncoding.Unknown:
                default:
                    return EncodingExtensions.PdfDefault;
            }
        }

        private int GetCharacterCodeLength(PdfFontBase font)
        {
            if (font == null)
            {
                _logger.LogWarning("Font is null in GetCharacterCodeLength, assuming 1-byte codes.");
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
                    _logger.LogWarning("Unknown font type {Type}, using encoding-based code length.", font.GetType().Name);
                    return GetCharacterCodeLengthByEncoding(font.Encoding, font);
            }
        }

        private int GetCharacterCodeLengthForComposite(PdfCompositeFont compositeFont)
        {
            var byEncoding = GetCharacterCodeLengthByEncoding(compositeFont.Encoding, compositeFont);
            if (byEncoding != 1)
            {
                return byEncoding;
            }
            return 2;
        }

        private int GetCharacterCodeLengthForCID(PdfCIDFont cidFont)
        {
            _logger.LogInformation("CID font {Font} accessed directly - should typically be accessed through composite font.", cidFont.BaseFont);
            return 2;
        }

        private int GetCharacterCodeLengthForSimple(PdfSimpleFont simpleFont)
        {
            var byEncoding = GetCharacterCodeLengthByEncoding(simpleFont.Encoding, simpleFont);
            if (byEncoding == 2)
            {
                _logger.LogWarning("Simple font {Font} has 2-byte encoding {Encoding}, but simple fonts should use 1-byte codes.", simpleFont.BaseFont, simpleFont.Encoding);
            }
            return 1;
        }

        private int GetCharacterCodeLengthForType3Font(PdfType3Font type3Font)
        {
            return 1;
        }

        private int GetCharacterCodeLengthByEncoding(PdfFontEncoding encoding, PdfFontBase font)
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
                    _logger.LogWarning("Custom encoding for font {Font} - assuming 1-byte codes. Name={Custom}.", font.BaseFont, font.CustomEncoding);
                    return 1;
                case PdfFontEncoding.Unknown:
                default:
                    return 1;
            }
        }
    }
}