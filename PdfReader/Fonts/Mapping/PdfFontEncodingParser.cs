using System.Collections.Generic;
using PdfReader.Fonts.Types;
using PdfReader.Models;

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
                return new PdfFontEncodingInfo(PdfFontEncoding.Unknown, null, null);
            }

            // Name case: /Encoding /WinAnsiEncoding, /UniJIS-UTF16-H, etc.
            var name = encVal.AsName();
            if (!string.IsNullOrEmpty(name))
            {
                var encoding = name switch
                {
                    PdfTokens.MacRomanEncodingKey => PdfFontEncoding.MacRomanEncoding,
                    PdfTokens.WinAnsiEncodingKey => PdfFontEncoding.WinAnsiEncoding,
                    PdfTokens.MacExpertEncodingKey => PdfFontEncoding.MacExpertEncoding,
                    PdfTokens.IdentityHEncodingKey => PdfFontEncoding.IdentityH,
                    PdfTokens.IdentityVEncodingKey => PdfFontEncoding.IdentityV,
                    // Predefined Unicode CMaps (use PdfTokens constants)
                    PdfTokens.UniJIS_UTF16_H_EncodingKey => PdfFontEncoding.UniJIS_UTF16_H,
                    PdfTokens.UniJIS_UTF16_V_EncodingKey => PdfFontEncoding.UniJIS_UTF16_V,
                    PdfTokens.UniGB_UTF16_H_EncodingKey => PdfFontEncoding.UniGB_UTF16_H,
                    PdfTokens.UniGB_UTF16_V_EncodingKey => PdfFontEncoding.UniGB_UTF16_V,
                    PdfTokens.UniCNS_UTF16_H_EncodingKey => PdfFontEncoding.UniCNS_UTF16_H,
                    PdfTokens.UniCNS_UTF16_V_EncodingKey => PdfFontEncoding.UniCNS_UTF16_V,
                    PdfTokens.UniKS_UTF16_H_EncodingKey => PdfFontEncoding.UniKS_UTF16_H,
                    PdfTokens.UniKS_UTF16_V_EncodingKey => PdfFontEncoding.UniKS_UTF16_V,
                    _ => PdfFontEncoding.Custom
                };

                string custom = encoding == PdfFontEncoding.Custom ? name : null;
                return new PdfFontEncodingInfo(encoding, custom, null);
            }

            // Dictionary case: may include /BaseEncoding and /Differences
            var encDict = encVal.AsDictionary();
            if (encDict != null)
            {
                // Base encoding name (optional); default per spec is StandardEncoding for Type1/Type3, WinAnsi for TrueType
                var baseEncodingName = encDict.GetName(PdfTokens.BaseEncodingKey);
                var baseEncoding = baseEncodingName switch
                {
                    PdfTokens.StandardEncodingKey => PdfFontEncoding.StandardEncoding,
                    PdfTokens.MacRomanEncodingKey => PdfFontEncoding.MacRomanEncoding,
                    PdfTokens.WinAnsiEncodingKey => PdfFontEncoding.WinAnsiEncoding,
                    PdfTokens.MacExpertEncodingKey => PdfFontEncoding.MacExpertEncoding,
                    _ => PdfFontEncoding.StandardEncoding
                };

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
                            var n = item.AsName();
                            if (!string.IsNullOrEmpty(n) && n[0] == '/')
                            {
                                n = n.Substring(1);
                            }

                            differences[currentCode] = n;
                            currentCode++;
                        }
                    }
                }

                return new PdfFontEncodingInfo(baseEncoding, null, differences);
            }

            // Fallback: unknown encoding representation
            return new PdfFontEncodingInfo(PdfFontEncoding.Unknown, null, null);
        }
    }
}
