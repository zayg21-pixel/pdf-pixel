using System.Collections.Generic;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Static helper for parsing font encoding information from a PDF dictionary.
/// </summary>
internal static class PdfFontEncodingParser
{
    /// <summary>
    /// Parses /Encoding entry supporting both name and dictionary cases, including /Differences.
    /// Returns the resolved base encoding enum, optional custom name, and Differences map.
    /// </summary>
    /// <param name="dict">PDF dictionary containing the font definition.</param>
    /// <returns>Parsed encoding info.</returns>
    public static PdfFontEncodingInfo ParseSingleByteEncoding(PdfDictionary dict)
    {
        IPdfValue? encodingValue = dict.GetValue(PdfTokens.EncodingKey);
        if (encodingValue == null)
        {
            // No /Encoding specified, assume standard
            return new PdfFontEncodingInfo(PdfEncoding.Unknown, null, null);
        }

        // Name case: /Encoding /WinAnsiEncoding, /UniJIS-UTF16-H, etc.
        PdfString? encodingName = encodingValue.AsName();

        if (encodingName != null)
        {
            PdfEncoding encoding = encodingName.Value.AsEnum<PdfEncoding>();
            return new PdfFontEncodingInfo(encoding, encodingName.Value, null);
        }

        // Dictionary case: may include /BaseEncoding and /Differences
        PdfDictionary? encodingDictionary = encodingValue.AsDictionary();
        if (encodingDictionary != null)
        {
            // Base encoding name (optional); default per spec is StandardEncoding for Type1/Type3, WinAnsi for TrueType
            PdfString? baseEncodingName = encodingDictionary.GetName(PdfTokens.BaseEncodingKey);
            PdfEncoding baseEncoding = (baseEncodingName ?? PdfString.Empty).AsEnum<PdfEncoding>();

            Dictionary<int, PdfString> differences = [];
            PdfArray? diffs = encodingDictionary.GetArray(PdfTokens.DifferencesKey);

            if (diffs != null)
            {
                int currentCode = -1;
                for (int i = 0; i < diffs.Count; i++)
                {
                    IPdfValue? item = diffs.GetValue(i);

                    if (item == null)
                    {
                        continue;
                    }

                    int? differenceCode = item.AsInteger();
                    if (differenceCode != null)
                    {
                        currentCode = differenceCode.Value;
                        continue;
                    }

                    PdfString? differenceName = item.AsName();
                    if (differenceName != null && currentCode >= 0)
                    {
                        differences[currentCode] = differenceName.Value;
                        currentCode++;
                    }
                }
            }

            return new PdfFontEncodingInfo(baseEncoding, baseEncodingName, differences);
        }

        // Fallback: unknown encoding representation
        return new PdfFontEncodingInfo(PdfEncoding.Unknown, PdfString.Empty, null);
    }
}
