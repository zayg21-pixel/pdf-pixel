using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.PostScript;
using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfReader.Fonts.PsFont
{
    internal static class Type1ToCffConverter
    {
        public static byte[] GetCffFont(PdfFontDescriptor descriptor)
        {
            var file = descriptor.FontFileObject;
            var length1 = file.Dictionary.GetIntegerOrDefault((PdfString)"Length1"u8); // TODO: store properly
            var length2 = file.Dictionary.GetIntegerOrDefault((PdfString)"Length2"u8);
            var length3 = file.Dictionary.GetIntegerOrDefault((PdfString)"Length3"u8);
            var rawData = file.DecodeAsMemory();

            if (rawData.IsEmpty)
            {
                throw new InvalidDataException("Empty Type1 font stream.");
            }

            // Reject binary PFB wrapper (PDF should embed PFA style only).
            bool isBinaryPfb = rawData.Length >= 6 && rawData.Span[0] == 0x80 && rawData.Span[1] == 0x01;
            if (isBinaryPfb)
            {
                throw new InvalidDataException("Unsupported embedded binary PFB Type1 font stream; PDF requires PFA-style embedding.");
            }

            if (length1 <= 0 || length1 > rawData.Length)
            {
                throw new InvalidDataException("Invalid Length1 for Type1 font stream (spec compliance required).");
            }
            if (length2 <= 0 || length1 + length2 > rawData.Length)
            {
                throw new InvalidDataException("Invalid Length2 for Type1 font stream (spec compliance required).");
            }

            var parsedDictionary = ParseFontProgram(descriptor, rawData, length1, length2, file.Document.LoggerFactory);

            return Type1DictionaryToCffConverter.GenerateCffFontDataFromDictionary(parsedDictionary, descriptor);
        }

        private static PostScriptDictionary ParseFontProgram(PdfFontDescriptor descriptor, ReadOnlyMemory<byte> rawData, int length1, int length2, ILoggerFactory loggerFactory)
        {
            var operandStack = new Stack<PostScriptToken>();
            var headerSpan = rawData.Span.Slice(0, length1);

            var headerEvaluator = new PostScriptEvaluator(headerSpan, loggerFactory.CreateLogger<PostScriptEvaluator>());
            var fontDirectory = new PostScriptDictionary();

            headerEvaluator.SetSystemValue(PsFontDictionaryUtilities.FontDirectoryKey, fontDirectory);
            headerEvaluator.SetSystemValue(PsFontDictionaryUtilities.StandardEncodingName, PsFontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.StandardEncoding));
            headerEvaluator.SetSystemValue(PsFontDictionaryUtilities.MacRomanEncodingName, PsFontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacRomanEncoding));
            headerEvaluator.SetSystemValue(PsFontDictionaryUtilities.MacExpertEncodingName, PsFontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacExpertEncoding));
            headerEvaluator.SetSystemValue(PsFontDictionaryUtilities.WinAnsiEncodingName, PsFontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.WinAnsiEncoding));

            headerEvaluator.EvaluateTokens(operandStack);

            var encryptedSpan = rawData.Span.Slice(length1, length2);
            var decryptedSpan = Type1Decryptor.DecryptEexecBinary(encryptedSpan);

            var eexecEvaluator = new PostScriptEvaluator(decryptedSpan, loggerFactory.CreateLogger<PostScriptEvaluator>());
            eexecEvaluator.SetSystemValue(PsFontDictionaryUtilities.FontDirectoryKey, fontDirectory);
            eexecEvaluator.EvaluateTokens(operandStack);

            PostScriptDictionary fontDictionary = null;

            if (fontDirectory.Entries.TryGetValue(descriptor.FontName.ToString(), out var font))
            {
                fontDictionary = font as PostScriptDictionary;
            }
            else
            {
                // fallback: take the last defined font dictionary in FontDirectory
                fontDictionary = fontDirectory.Entries.Values.OfType<PostScriptDictionary>().LastOrDefault();
            }

            if (fontDictionary == null)
            {
                throw new InvalidDataException("Font dictionary missing after eexec execution.");
            }

            return fontDictionary;
        }
    }
}
