using System.Collections.Generic;
using System.Text;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Static helper for parsing font encoding information from a PDF dictionary.
    /// </summary>
    public static class PdfFontEncodingParser
    {
        /// <summary>
        /// Parses /Encoding entry supporting both name and dictionary cases, including /Differences.
        /// Returns the resolved base encoding enum (or Identity encodings for CID), optional custom name, and Differences map.
        /// </summary>
        /// <param name="dict">PDF dictionary containing the font definition.</param>
        /// <returns>Parsed encoding info.</returns>
        public static PdfFontEncodingInfo ParseEncoding(PdfDictionary dict)
        {
            var encVal = dict.GetValue(PdfTokens.EncodingKey);
            if (encVal == null)
            {
                // No /Encoding specified, assume standard
                return new PdfFontEncodingInfo(PdfFontEncoding.Unknown, PdfString.Empty, null);
            }

            // Name case: /Encoding /WinAnsiEncoding, /UniJIS-UTF16-H, etc.
            var name = encVal.AsName();

            if (!name.IsEmpty)
            {
                var encoding = name.AsEnum<PdfFontEncoding>();
                return new PdfFontEncodingInfo(encoding, name, null);
            }

            // Dictionary case: may include /BaseEncoding and /Differences
            var encDict = encVal.AsDictionary();
            if (encDict != null)
            {
                // Base encoding name (optional); default per spec is StandardEncoding for Type1/Type3, WinAnsi for TrueType
                var baseEncoding = encDict.GetName(PdfTokens.BaseEncodingKey).AsEnum<PdfFontEncoding>();

                var differences = new Dictionary<int, string>();
                var diffs = encDict.GetArray(PdfTokens.DifferencesKey);

                if (diffs != null)
                {
                    int currentCode = -1;
                    for (int i = 0; i < diffs.Count; i++)
                    {
                        var item = diffs.GetValue(i);

                        if (item == null)
                        {
                            continue;
                        }

                        if (item.Type == PdfValueType.Integer)
                        {
                            currentCode = item.AsInteger();
                        }
                        else if (item.Type == PdfValueType.Name && currentCode >= 0)
                        {
                            differences[currentCode] = item.AsName().ToString();
                            currentCode++;
                        }
                    }
                }

                return new PdfFontEncodingInfo(baseEncoding, PdfString.Empty, differences);
            }

            // Fallback: unknown encoding representation
            return new PdfFontEncodingInfo(PdfFontEncoding.Unknown, PdfString.Empty, null);
        }
    }
}
