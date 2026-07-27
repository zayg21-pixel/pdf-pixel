using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.PostScript;
using PdfPixel.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Converts a Type1 font program embedded in a PDF to CFF font data.
/// </summary>
public static class Type1ToCffConverter
{
    /// <summary>
    /// Retrieves the CFF font data from a raw Type1 font program.
    /// </summary>
    /// <param name="rawProgram">The raw Type1 font program, either PFA-style or (spec violation, but
    /// tolerated) PFB-wrapped -- for a PFB-wrapped program, <see cref="Type1RawFontProgram.Length1"/>/
    /// <see cref="Type1RawFontProgram.Length2"/> are ignored in favor of its own segment lengths.</param>
    /// <param name="loggerFactory">Logger factory for PostScript evaluation diagnostics.</param>
    /// <returns>The parsed Type1 font as a structured CFF typeface.</returns>
    /// <exception cref="InvalidDataException">Invalid font data.</exception>
    public static CffTypeface? GetCffFont(in Type1RawFontProgram rawProgram, ILoggerFactory loggerFactory)
    {
        if (rawProgram.Data.IsEmpty)
        {
            throw new InvalidDataException("Empty Type1 font stream.");
        }

        ReadOnlySpan<byte> rawSpan = rawProgram.Data.Span;
        bool isBinaryPfb = rawSpan.Length >= 6 && rawSpan[0] == 0x80 && rawSpan[1] == 0x01;
        Type1RawFontProgram program = isBinaryPfb
            ? Type1PfbSegmentReader.ExtractSegments(rawProgram.Data)
            : rawProgram;

        if (program.Length1 <= 0 || program.Length1 > program.Data.Length)
        {
            throw new InvalidDataException("Invalid Length1 for Type1 font stream (spec compliance required).");
        }

        if (program.Length2 <= 0 || program.Length1 + program.Length2 > program.Data.Length)
        {
            throw new InvalidDataException("Invalid Length2 for Type1 font stream (spec compliance required).");
        }

        PostScriptDictionary parsedDictionary = ParseFontProgram(program, loggerFactory);

        return Type1DictionaryToCffConverter.GenerateCffTypefaceFromDictionary(parsedDictionary, loggerFactory);
    }

    private static PostScriptDictionary ParseFontProgram(in Type1RawFontProgram program, ILoggerFactory loggerFactory)
    {
        Stack<PostScriptToken> operandStack = [];
        ReadOnlySpan<byte> headerSpan = program.Data.Span.Slice(0, program.Length1);

        PostScriptEvaluator headerEvaluator = new(headerSpan, appendExec: false, loggerFactory.CreateLogger<PostScriptEvaluator>());
        PostScriptDictionary fontDirectory = new();

        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.FontDirectoryKey, fontDirectory);
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.StandardEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.StandardEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.MacRomanEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacRomanEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.MacExpertEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.MacExpertEncoding));
        headerEvaluator.SetSystemValue(Type1FontDictionaryUtilities.WinAnsiEncodingName, Type1FontDictionaryUtilities.GetEncodingArray(PdfFontEncoding.WinAnsiEncoding));

        headerEvaluator.EvaluateTokens(operandStack);

        ReadOnlySpan<byte> encryptedSpan = program.Data.Span.Slice(program.Length1, program.Length2);
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
