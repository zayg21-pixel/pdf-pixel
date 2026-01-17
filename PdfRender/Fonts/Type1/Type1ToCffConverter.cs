using Microsoft.Extensions.Logging;
using PdfRender.Fonts.Cff;
using PdfRender.Fonts.Model;
using PdfRender.PostScript;
using PdfRender.PostScript.Tokens;
using PdfRender.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfRender.Fonts.Type1;

/// <summary>
/// Converts a Type1 font program embedded in a PDF to CFF font data.
/// </summary>
internal static class Type1ToCffConverter
{
    /// <summary>
    /// Retrieves the CFF font data from a Type1 font program embedded in the given font descriptor.
    /// </summary>
    /// <param name="descriptor">Font instance.</param>
    /// <returns>CFF font bytes.</returns>
    /// <exception cref="InvalidDataException">Invalid font data.</exception>
    public static CffInfo GetCffFont(PdfFontDescriptor descriptor)
    {
        if (descriptor?.FontFileObject == null)
        {
            throw new InvalidDataException("Missing font file for Type1 font.");
        }

        var file = descriptor.FontFileObject;
        var length1 = file.Dictionary.GetIntegerOrDefault(PdfTokens.Length1);
        var length2 = file.Dictionary.GetIntegerOrDefault(PdfTokens.Length2);
        var length3 = file.Dictionary.GetIntegerOrDefault(PdfTokens.Length3);
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

        var headerEvaluator = new PostScriptEvaluator(headerSpan, appendExec: false, loggerFactory.CreateLogger<PostScriptEvaluator>());
        var fontDirectory = new PostScriptDictionary();

        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.FontDirectoryKey, fontDirectory);
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.StandardEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.StandardEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.MacRomanEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacRomanEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.MacExpertEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacExpertEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.WinAnsiEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.WinAnsiEncoding));

        headerEvaluator.EvaluateTokens(operandStack);

        var encryptedSpan = rawData.Span.Slice(length1, length2);
        var decryptedSpan = Type1Decryptor.DecryptEexecBinary(encryptedSpan);

        var eexecEvaluator = new PostScriptEvaluator(decryptedSpan, appendExec: false, loggerFactory.CreateLogger<PostScriptEvaluator>());
        eexecEvaluator.SetSystemValue(Type1FontDictionaryUtilities.FontDirectoryKey, fontDirectory);
        eexecEvaluator.EvaluateTokens(operandStack);

        PostScriptDictionary fontResources = eexecEvaluator.GetResourceCategory(PostScriptEvaluator.FontResourceCategory);
        var fontDictionary = fontResources?.Entries.FirstOrDefault().Value as PostScriptDictionary;

        if (fontDictionary == null)
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
