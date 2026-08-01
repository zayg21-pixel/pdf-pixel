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
    private static ReadOnlySpan<byte> EexecSignature => "eexec"u8;

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

        program = ResolveEexecBoundary(program);

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

    // Some PDF producers miscount /Length1, landing a few bytes short of where the "eexec" keyword
    // (plus its trailing whitespace) actually ends. Since eexec is a stream cipher, decrypting from
    // the wrong offset desyncs the entire rest of the font, so the declared boundary is verified
    // against where "eexec" is actually found and corrected if they disagree; Length2 shifts by the
    // same amount so the two lengths still cover exactly the span they did before correction.
    private static Type1RawFontProgram ResolveEexecBoundary(in Type1RawFontProgram program)
    {
        ReadOnlySpan<byte> data = program.Data.Span;

        if (program.Length1 > 0 && program.Length1 <= data.Length && EndsWithEexecSignature(data, program.Length1))
        {
            return program;
        }

        int actualHeaderLength = FindEexecBodyStart(data, 0);

        if (actualHeaderLength < 0)
        {
            return program;
        }

        int delta = actualHeaderLength - program.Length1;

        return new Type1RawFontProgram(program.Data, actualHeaderLength, program.Length2 - delta);
    }

    private static bool EndsWithEexecSignature(in ReadOnlySpan<byte> data, int declaredHeaderLength)
    {
        ReadOnlySpan<byte> declaredHeader = data.Slice(0, declaredHeaderLength);
        int windowStart = Math.Max(0, declaredHeaderLength - (2 * EexecSignature.Length));

        return FindEexecBodyStart(declaredHeader, windowStart) == declaredHeaderLength;
    }

    // Locates the byte immediately following "eexec" and any whitespace after it, starting the
    // search at startIndex. Returns -1 if the signature isn't present.
    private static int FindEexecBodyStart(in ReadOnlySpan<byte> data, int startIndex)
    {
        if (startIndex >= data.Length)
        {
            return -1;
        }

        int index = data.Slice(startIndex).IndexOf(EexecSignature);

        if (index < 0)
        {
            return -1;
        }

        int position = startIndex + index + EexecSignature.Length;

        while (position < data.Length && IsEexecWhitespace(data[position]))
        {
            position++;
        }

        return position;
    }

    private static bool IsEexecWhitespace(byte value) => value == 0x20 || value == 0x09 || value == 0x0D || value == 0x0A;
}
