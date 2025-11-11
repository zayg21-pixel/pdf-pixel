using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Resources;
using System.Collections.Generic;

namespace PdfReader.Text
{
    /// <summary>
    /// Utility for converting single-byte PDF codes with base encodings
    /// to standard glyph names.
    /// </summary>
    internal static class SingleByteEncodings
    {
        public static readonly PdfString UndefinedCharacter = (PdfString)".notdef"u8;

        static SingleByteEncodings()
        {
            var standardData = PdfResourceLoader.GetResource("StandardEncodings.bin");
            standard = PdfTextResourceConverter.FromPdfStringBlob(standardData);

            var ansiData = PdfResourceLoader.GetResource("AnsiEncodings.bin");
            ansi = PdfTextResourceConverter.FromPdfStringBlob(ansiData);

            var macRomanData = PdfResourceLoader.GetResource("MacRomanEncodings.bin");
            macRoman = PdfTextResourceConverter.FromPdfStringBlob(macRomanData);

            var macExpertData = PdfResourceLoader.GetResource("MacExpertEncodings.bin");
            macExpert = PdfTextResourceConverter.FromPdfStringBlob(macExpertData);
        }


        private static readonly PdfString[] standard;
        private static readonly PdfString[] ansi;
        private static readonly PdfString[] macRoman;
        private static readonly PdfString[] macExpert;

        public static PdfString[] GetEncodingSet(PdfFontEncoding encoding)
        {
            return encoding switch
            {
                PdfFontEncoding.StandardEncoding => standard,
                PdfFontEncoding.WinAnsiEncoding => ansi,
                PdfFontEncoding.MacExpertEncoding => macExpert,
                PdfFontEncoding.MacRomanEncoding => macRoman,
                _ => default,
            };
        }

        public static PdfString GetNameByCode(byte code, PdfFontEncoding encoding, Dictionary<int, PdfString> differences = default)
        {
            if (differences != null && differences.TryGetValue(code, out PdfString name) && !name.IsEmpty)
            {
                return name;
            }

            return encoding switch
            {
                PdfFontEncoding.StandardEncoding => standard[code],
                PdfFontEncoding.MacRomanEncoding => macRoman[code],
                PdfFontEncoding.WinAnsiEncoding => ansi[code],
                PdfFontEncoding.MacExpertEncoding => macExpert[code],
                _ => default,
            };
        }

        public static PdfString GetNameByCodeOrUndefined(byte code, PdfFontEncoding encoding, Dictionary<int, PdfString> differences = default)
        {
            var result = GetNameByCode(code, encoding, differences);

            if (result.IsEmpty)
            {
                return UndefinedCharacter;
            }
            else
            {
                return result;
            }
        }
    }
}
