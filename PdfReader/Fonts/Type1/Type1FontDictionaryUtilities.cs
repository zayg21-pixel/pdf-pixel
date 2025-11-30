using System;
using System.Collections.Generic;
using PdfReader.PostScript.Tokens;
using PdfReader.Models;
using PdfReader.Text;
using PdfReader.Fonts.Model;

namespace PdfReader.Fonts.Type1;

/// <summary>
/// Helper utilities for extracting common Type1 font dictionary entries (LenIV, CharStrings, FontMatrix).
/// Uses symbolic constants for key names.
/// </summary>
internal static class Type1FontDictionaryUtilities
{
    public const string StandardEncodingName = "StandardEncoding";
    public const string MacRomanEncodingName = "MacRomanEncoding";
    public const string WinAnsiEncodingName = "WinAnsiEncoding";
    public const string MacExpertEncodingName = "MacExpertEncoding";
    public const string FontDirectoryKey = "FontDirectory";
    private const string NotdefName = ".notdef"; // Constant for .notdef glyph name.

    private const string FontNameKey = "FontName";
    private const string PrivateKey = "Private";
    private const string LenIVKey = "lenIV";
    private const string CharStringsKey = "CharStrings";
    private const string FontMatrixKey = "FontMatrix";
    private const string EncodingKey = "Encoding";
    private const string SubrsKey = "Subrs";
    private const string FontBboxKey = "FontBBox";

    /// <summary>
    /// Extract the LenIV value from a font dictionary's /Private sub-dictionary if present.
    /// Defaults to 4 when absent or invalid (spec default).
    /// </summary>
    /// <param name="fontDict">Top-level font dictionary.</param>
    /// <returns>LenIV integer (0..16 typical). -1 indicates unencrypted CharStrings/Subrs.</returns>
    public static int GetLenIV(PostScriptDictionary fontDict)
    {
        if (fontDict == null)
        {
            return 4; // Default
        }
        if (fontDict.Entries.TryGetValue(PrivateKey, out PostScriptToken privateToken) && privateToken is PostScriptDictionary privateDict)
        {
            if (privateDict.Entries.TryGetValue(LenIVKey, out PostScriptToken lenIvToken) && lenIvToken is PostScriptNumber num)
            {
                int candidate = (int)num.Value;
                if (candidate >= -1 && candidate <= 32)
                {
                    return candidate;
                }
            }
        }
        return 4;
    }

    /// <summary>
    /// Retrieves the font name from the specified PostScript dictionary.
    /// </summary>
    /// <param name="fontDict">The PostScript dictionary from which to extract the font name. Cannot be null.</param>
    /// <returns>The font name as a string if found; otherwise, <see langword="null"/>.</returns>
    public static string GetFontName(PostScriptDictionary fontDict)
    {
        if (fontDict == null)
        {
            return null;
        }

        if (fontDict.Entries.TryGetValue(FontNameKey, out PostScriptToken token))
        {
            if (token is PostScriptLiteralName ln)
            {
                return ln.Name;
            }
        }
        return null;
    }

    /// <summary>
    /// Extract decrypted CharStrings from /CharStrings dictionary. Each value is obtained from a PostScriptBinaryString
    /// token (raw encrypted bytes) and decrypted immediately using LenIV. Returns empty dictionary if /CharStrings absent.
    /// </summary>
    /// <param name="fontDict">Top-level font dictionary.</param>
    /// <returns>Dictionary of glyph name to decrypted charstring bytes (LenIV prefix removed when applicable).</returns>
    public static Dictionary<PdfString, byte[]> GetCharStrings(PostScriptDictionary fontDict)
    {
        var results = new Dictionary<PdfString, byte[]>();
        if (fontDict == null)
        {
            return results;
        }
        if (!fontDict.Entries.TryGetValue(CharStringsKey, out PostScriptToken csToken) || csToken is not PostScriptDictionary csDict)
        {
            return results;
        }
        int lenIV = GetLenIV(fontDict);
        foreach (KeyValuePair<string, PostScriptToken> entry in csDict.Entries)
        {
            if (entry.Value is PostScriptBinaryString bin)
            {
                byte[] data = bin.Data ?? Array.Empty<byte>();
                byte[] decrypted = lenIV < 0 ? data : Type1Decryptor.DecryptCharString(data, lenIV);
                results[(PdfString)entry.Key] = decrypted;
            }
        }
        return results;
    }

    /// <summary>
    /// Extract decrypted subroutine charstrings from the /Private dictionary's /Subrs array.
    /// Returns an index->byte[] mapping (decrypted bytes). Returns an empty dictionary when
    /// absent or malformed.
    /// </summary>
    /// <param name="fontDict">Top-level font dictionary.</param>
    /// <returns>Dictionary mapping subroutine index to decrypted byte array.</returns>
    public static Dictionary<int, byte[]> GetSubroutines(PostScriptDictionary fontDict)
    {
        var subrs = new Dictionary<int, byte[]>(capacity: 32);
        if (fontDict == null)
        {
            return subrs;
        }
        if (!fontDict.Entries.TryGetValue(PrivateKey, out PostScriptToken privateToken) || privateToken is not PostScriptDictionary privateDict)
        {
            return subrs;
        }
        if (!privateDict.Entries.TryGetValue(SubrsKey, out PostScriptToken subrsToken) || subrsToken is not IPostScriptCollection subrsArray)
        {
            return subrs;
        }

        int lenIV = GetLenIV(fontDict);
        for (int i = 0; i < subrsArray.Items.Count; i++)
        {
            PostScriptToken element = subrsArray.Items[i];
            if (element is PostScriptBinaryString bin)
            {
                byte[] data = bin.Data ?? Array.Empty<byte>();
                var decrypted = lenIV < 0 ? data : Type1Decryptor.DecryptCharString(data, lenIV);
                subrs[i] = decrypted;
            }
        }
        return subrs;
    }

    /// <summary>
    /// Retrieves the bounding box of a font from the specified PostScript dictionary.
    /// </summary>
    /// <param name="dict">The PostScript dictionary containing font information. Must not be <see langword="null"/>.</param>
    /// <returns>An array of four <see cref="float"/> values representing the font's bounding box, or <see langword="null"/> if
    /// the dictionary is <see langword="null"/> or does not contain valid bounding box data.</returns>
    public static float[] GetFontBBox(PostScriptDictionary dict)
    {
        if (dict == null)
        {
            return null;
        }

        if (dict.Entries.TryGetValue(FontBboxKey, out PostScriptToken token) && token is IPostScriptCollection arr && arr.Items != null && arr.Items.Count >= 4)
        {
            var result = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (arr.Items[i] is PostScriptNumber num)
                {
                    result[i] = num.Value;
                }
                else
                {
                    result[i] = 0f;
                }
            }
            return result;
        }
        return null;
    }

    /// <summary>
    /// Extract FontMatrix (array of 6 numbers) if present. Returns null when absent or malformed.
    /// </summary>
    /// <param name="fontDict">Top-level font dictionary.</param>
    /// <returns>SKMatrix representing the font's transformation matrix.</returns>
    public static float[] GetFontMatrix(PostScriptDictionary fontDict)
    {
        if (fontDict == null)
        {
            return null;
        }
        if (!fontDict.Entries.TryGetValue(FontMatrixKey, out PostScriptToken fmToken) || fmToken is not PostScriptArray array)
        {
            return null;
        }
        if (array.Elements == null || array.Elements.Length < 6)
        {
            return null;
        }
        float[] values = new float[6];
        int count = 0;
        foreach (PostScriptToken element in array.Elements)
        {
            if (element is PostScriptNumber num && count < 6)
            {
                values[count++] = num.Value;
            }
        }

        return values;
    }

    /// <summary>
    /// Extract a256-entry encoding vector. If /Encoding is a known predefined name, returns the shared vector
    /// from <see cref="SingleByteEncodings"/>. If /Encoding is an array (custom), builds a new256-length vector
    /// mapping indices to glyph names. Entries equal to ".notdef" are normalized to PdfString.Empty so callers can compare using SingleByteEncodings.UndefinedCharacter.
    /// Returns null if /Encoding absent or unknown.
    /// </summary>
    /// <param name="fontDict">Top-level font dictionary.</param>
    /// <returns>PdfString[256] encoding vector or null if not present.</returns>
    public static PdfString[] GetEncodingVector(PostScriptDictionary fontDict)
    {
        if (fontDict == null)
        {
            return null;
        }
        if (!fontDict.Entries.TryGetValue(EncodingKey, out PostScriptToken encToken))
        {
            return null;
        }
        if (encToken is PostScriptLiteralName litName)
        {
            // Map predefined encoding name to enum then to shared vector.
            PdfFontEncoding encodingEnum = MapEncodingName(litName.Name);
            PdfString[] predefined = SingleByteEncodings.GetEncodingSet(encodingEnum);
            return predefined; // May be null if unknown (caller handles null).
        }
        if (encToken is PostScriptArray arr && arr.Elements != null)
        {
            var vector = new PdfString[256];
            int limit = Math.Min(arr.Elements.Length, 256);
            for (int i = 0; i < limit; i++)
            {
                PostScriptToken element = arr.Elements[i];
                if (element is PostScriptLiteralName glyphName)
                {
                    vector[i] = glyphName.Name == NotdefName ? PdfString.Empty : (PdfString)glyphName.Name;
                }
                else
                {
                    vector[i] = PdfString.Empty;
                }
            }
            return vector;
        }
        return null;
    }

    private static PdfFontEncoding MapEncodingName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return PdfFontEncoding.Unknown;
        }
        // Try direct enum parse first (names match enum identifiers for known encodings).
        if (Enum.TryParse(name, ignoreCase: false, out PdfFontEncoding parsed))
        {
            return parsed;
        }
        // Common PostScript variants that may differ (add mappings as needed).
        switch (name)
        {
            case StandardEncodingName:
                return PdfFontEncoding.StandardEncoding;
            case MacRomanEncodingName:
                return PdfFontEncoding.MacRomanEncoding;
            case WinAnsiEncodingName:
                return PdfFontEncoding.WinAnsiEncoding;
            case MacExpertEncodingName:
                return PdfFontEncoding.MacExpertEncoding;
            default:
                return PdfFontEncoding.Unknown;
        }
    }

    public static PostScriptArray GetEncodingArray(PdfFontEncoding pdfEncoding)
    {
        var encodingSet = SingleByteEncodings.GetEncodingSet(pdfEncoding);

        if (encodingSet == null)
        {
            return null;
        }

        var encodingArray = new PostScriptArray(encodingSet.Length);

        for (int i = 0; i < encodingSet.Length; i++)
        {
            var glyphName = encodingSet[i];
            if (glyphName == PdfString.Empty)
            {
                encodingArray.Elements[i] = new PostScriptLiteralName(NotdefName);
            }
            else
            {
                encodingArray.Elements[i] = new PostScriptLiteralName(glyphName.ToString());
            }
        }

        return encodingArray;
    }
}
