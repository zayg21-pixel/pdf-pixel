using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.PostScript;
using PdfPixel.PostScript.Tokens;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Converts a Type1 font program embedded in a PDF to CFF font data.
/// </summary>
internal static class Type1ToCffConverter
{
    /// <summary>
    /// Retrieves the CFF font data from a Type1 font program embedded in the given font descriptor.
    /// </summary>
    /// <param name="descriptor">Font instance.</param>
    /// <param name="loggerFactory">Logger factory for PostScript evaluation diagnostics.</param>
    /// <returns>CFF font bytes.</returns>
    /// <exception cref="InvalidDataException">Invalid font data.</exception>
    public static CffInfo? GetCffFont(PdfFontDescriptor descriptor, ILoggerFactory loggerFactory)
    {
        if (descriptor?.FontFileStream == null)
        {
            throw new InvalidDataException("Missing font file for Type1 font.");
        }

        int length1 = descriptor.FontFileLength1;
        int length2 = descriptor.FontFileLength2;
        ReadOnlyMemory<byte> rawData = descriptor.FontFileStream.DecodeAsMemory();

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

        PostScriptDictionary parsedDictionary = ParseFontProgram(descriptor, rawData, length1, length2, loggerFactory);

        return Type1DictionaryToCffConverter.GenerateCffFontDataFromDictionary(parsedDictionary, descriptor);
    }

    private static PostScriptDictionary ParseFontProgram(PdfFontDescriptor descriptor, in ReadOnlyMemory<byte> rawData, int length1, int length2, ILoggerFactory loggerFactory)
    {
        Stack<PostScriptToken> operandStack = [];
        ReadOnlySpan<byte> headerSpan = rawData.Span.Slice(0, length1);

        PostScriptEvaluator headerEvaluator = new(headerSpan, appendExec: false, loggerFactory.CreateLogger<PostScriptEvaluator>());
        PostScriptDictionary fontDirectory = new();

        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.FontDirectoryKey, fontDirectory);
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.StandardEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.StandardEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.MacRomanEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacRomanEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.MacExpertEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacExpertEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.WinAnsiEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.WinAnsiEncoding));

        headerEvaluator.EvaluateTokens(operandStack);

        ReadOnlySpan<byte> encryptedSpan = rawData.Span.Slice(length1, length2);
        ReadOnlySpan<byte> decryptedSpan = Type1Decryptor.DecryptEexecBinary(encryptedSpan);

        PostScriptEvaluator eexecEvaluator = new(decryptedSpan, appendExec: false, loggerFactory.CreateLogger<PostScriptEvaluator>());
        eexecEvaluator.SetSystemValue(Type1FontDictionaryUtilities.FontDirectoryKey, fontDirectory);
        eexecEvaluator.EvaluateTokens(operandStack);

        PostScriptDictionary? fontResources = eexecEvaluator.GetResourceCategory(PostScriptEvaluator.FontResourceCategory);
        PostScriptDictionary? fontDictionary = fontResources?.Entries.FirstOrDefault().Value as PostScriptDictionary ?? fontDirectory.Entries.Values.OfType<PostScriptDictionary>().LastOrDefault();

        if (fontDictionary == null)
        {
            throw new InvalidDataException("Font dictionary missing after eexec execution.");
        }

        return fontDictionary;
    }
}
