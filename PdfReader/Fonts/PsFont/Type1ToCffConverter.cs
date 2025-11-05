using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Cff;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.PostScript;
using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfReader.Fonts.PsFont
{
    internal static class Type1ToCffConverter
    {
        /// <summary>
        /// Construct extractor for a spec-compliant embedded Type1 font stream.
        /// </summary>
        /// <param name="fontFile">PDF font stream object.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        /// <exception cref="InvalidDataException">Thrown on invalid or unsupported font stream.</exception>
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

            var parsedDictionary = ParseFontProgram(rawData, length1, length2, file.Document.LoggerFactory);

            return Type1Interpreter.GenerateCffFontData(parsedDictionary, descriptor);
        }

        private static PostScriptDictionary ParseFontProgram(ReadOnlyMemory<byte> rawData, int length1, int length2, ILoggerFactory loggerFactory)
        {
            var operandStack = new Stack<PostScriptToken>();

            var headerSpan = rawData.Span.Slice(0, length1);
            var headerEvaluator = new PostScriptEvaluator(headerSpan, loggerFactory.CreateLogger<PostScriptEvaluator>());
            headerEvaluator.SetSystemValue("FontDirectory", new PostScriptDictionary());
            headerEvaluator.EvaluateTokens(operandStack);

            var fontDict = operandStack.Pop() as PostScriptDictionary;
            if (fontDict == null)
            {
                throw new InvalidDataException("Header execution did not produce a font dictionary.");
            }
            operandStack.Push(fontDict); // Keep font dictionary for eexec modifications.

            var encryptedSpan = rawData.Span.Slice(length1, length2);
            var decryptedSpan = Type1Decryptor.DecryptEexecBinary(encryptedSpan);
            var eexecEvaluator = new PostScriptEvaluator(decryptedSpan, loggerFactory.CreateLogger<PostScriptEvaluator>());
            eexecEvaluator.SetSystemValue("FontDirectory", new PostScriptDictionary());
            eexecEvaluator.EvaluateTokens(operandStack);

            // Retrieve (possibly mutated) font dictionary reference.
            foreach (var token in operandStack)
            {
                if (token is PostScriptDictionary d)
                {
                    fontDict = d;
                    break;
                }
            }
            if (fontDict == null)
            {
                throw new InvalidDataException("Font dictionary missing after eexec execution.");
            }
            return fontDict;
        }
    }
}
