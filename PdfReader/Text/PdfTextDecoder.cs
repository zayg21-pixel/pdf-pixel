using PdfReader.Fonts;
using PdfReader.Fonts.Mapping;
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

            if (font.Differences != null && font.Differences.TryGetValue((int)(uint)code, out var charName))
            {
                if (AdobeGlyphList.CharacterMap.TryGetValue(charName, out var unicode))
                {
                    return unicode;
                }
            }

            var encoding = GetEncodingForFont(font);

            if (encoding != null)
            {
                try
                {
                    return encoding.GetString(code.Bytes);
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

            return EncodingExtensions.PdfDefault.GetString(code.Bytes);
        }

        /// <summary>
        /// Gets the encoding for the specified font using its encoding type.
        /// </summary>
        /// <param name="font">The font to get the encoding for.</param>
        /// <returns>The resolved encoding, or null if not available.</returns>
        private Encoding GetEncodingForFont(PdfFontBase font)
        {
            if (font == null)
            {
                _logger.LogWarning("Cannot determine encoding for null font.");
                return null;
            }

            switch (font.Encoding)
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
                    _logger.LogInformation("{Encoding} encountered for {Font}. Using UTF-16BE as best-effort when ToUnicode is absent.", font.Encoding, font.BaseFont);
                    return Encoding.BigEndianUnicode;
                case PdfFontEncoding.Custom:
                    _logger.LogWarning("Custom encoding for font {Font} not fully supported, using ISO-8859-1. Name={Custom}.", font.BaseFont, font.CustomEncoding);
                    return EncodingExtensions.PdfDefault;
                case PdfFontEncoding.Unknown:
                default:
                    return EncodingExtensions.PdfDefault;
            }
        }
    }
}