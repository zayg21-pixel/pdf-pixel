using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Streams;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Base class for all PDF font types with common properties and interface
    /// Provides the foundation for the proper font hierarchy according to PDF specification
    /// Essential properties are read-only and set through constructor for immutability
    /// Heavy operations are lazy-loaded using thread-safe Lazy&lt;T&gt; pattern
    /// </summary>
    public abstract class PdfFontBase
    {
        private readonly Lazy<PdfToUnicodeCMap> _toUnicodeCMap;

        /// <summary>
        /// Constructor for all PDF fonts with essential immutable properties
        /// Performs only lightweight dictionary operations
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        protected PdfFontBase(PdfDictionary fontDictionary)
        {
            Dictionary = fontDictionary ?? throw new ArgumentNullException(nameof(fontDictionary));

            // Parse encoding and differences from /Encoding (handles name or dictionary cases)
            var parsed = ParseEncoding(fontDictionary);
            Encoding = parsed.Encoding;
            CustomEncoding = parsed.CustomEncoding;
            Differences = parsed.Differences ?? new Dictionary<int, string>();

            // Parse essential properties from the font object (lightweight operations)
            var subtype = fontDictionary.GetName(PdfTokens.SubtypeKey);
            Type = ParseFontType(subtype);
            BaseFont = fontDictionary.GetString(PdfTokens.BaseFontKey) ?? string.Empty;
            
            // Initialize lazy loaders (thread-safe)
            _toUnicodeCMap = new Lazy<PdfToUnicodeCMap>(LoadToUnicodeCMap, isThreadSafe: true);
        }

        /// <summary>
        /// Font dictionary.
        /// </summary>
        public PdfDictionary Dictionary { get; }

        /// <summary>
        /// PDF font type (Type1, TrueType, Type3, Type0, CIDFontType0, CIDFontType2, etc.)
        /// Immutable - parsed from font object
        /// </summary>
        public PdfFontType Type { get; }

        /// <summary>
        /// Character encoding for this font (base encoding or CMap name type for CID fonts)
        /// </summary>
        public PdfFontEncoding Encoding { get; }

        /// <summary>
        /// Custom encoding name (when Encoding == Custom). For name-based encodings not recognized.
        /// </summary>
        public string CustomEncoding { get; }

        /// <summary>
        /// Differences array parsed from /Encoding dictionary as a code -> glyph name map.
        /// Empty for name-based encodings or when not present.
        /// </summary>
        public Dictionary<int, string> Differences { get; }

        /// <summary>
        /// Base font name (PostScript name)
        /// Immutable - parsed from font object
        /// </summary>
        public string BaseFont { get; }
        
        /// <summary>
        /// PDF document containing this font (convenience property)
        /// </summary>
        public PdfDocument Document => Dictionary.Document;
        
        /// <summary>
        /// Loaded ToUnicode CMap for character-to-Unicode mapping
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public PdfToUnicodeCMap ToUnicodeCMap => _toUnicodeCMap.Value;

        /// <summary>
        /// Check if this font has embedded font data
        /// </summary>
        public abstract bool IsEmbedded { get; }
        
        /// <summary>
        /// Get the font descriptor (contains metrics and embedding info)
        /// May be direct or inherited from descendant fonts
        /// Implementation may use lazy loading
        /// </summary>
        public abstract PdfFontDescriptor FontDescriptor { get; }
        
        /// <summary>
        /// Get the width of a character/glyph
        /// Implementation varies by font type
        /// </summary>
        public abstract float GetGlyphWidth(int charCode);

        /// <summary>
        /// Parse font type from PDF subtype string (lightweight operation)
        /// </summary>
        private static PdfFontType ParseFontType(string subtype)
        {
            return subtype switch
            {
                PdfTokens.Type1FontKey => PdfFontType.Type1,
                PdfTokens.TrueTypeFontKey => PdfFontType.TrueType,
                PdfTokens.Type3FontKey => PdfFontType.Type3,
                PdfTokens.Type0FontKey => PdfFontType.Type0,
                PdfTokens.CIDFontType0Key => PdfFontType.CIDFontType0,
                PdfTokens.CIDFontType2Key => PdfFontType.CIDFontType2,
                PdfTokens.MMType1FontKey => PdfFontType.MMType1,
                _ => PdfFontType.Unknown
            };
        }

        /// <summary>
        /// Parse /Encoding entry supporting both name and dictionary cases, including /Differences
        /// Returns the resolved base encoding enum (or Identity encodings for CID), optional custom name, and Differences map.
        /// </summary>
        private static (PdfFontEncoding Encoding, string CustomEncoding, Dictionary<int, string> Differences) ParseEncoding(PdfDictionary dict)
        {
            var encVal = dict.GetValue(PdfTokens.EncodingKey);
            if (encVal == null)
            {
                // No /Encoding specified
                return (PdfFontEncoding.Unknown, null, null);
            }

            // Name case: /Encoding /WinAnsiEncoding, /UniJIS-UTF16-H, etc.
            var name = encVal.AsName();
            if (!string.IsNullOrEmpty(name))
            {
                var encoding = name switch
                {
                    PdfTokens.StandardEncodingKey => PdfFontEncoding.StandardEncoding,
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
                return (encoding, custom, null);
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

                return (baseEncoding, null, differences);
            }

            // Fallback: unknown encoding representation
            return (PdfFontEncoding.Unknown, null, null);
        }

        /// <summary>
        /// Load ToUnicode CMap (heavy operation - lazy loaded using GetPageObject)
        /// </summary>
        private PdfToUnicodeCMap LoadToUnicodeCMap()
        {
            // Use GetPageObject instead of storing reference
            var toUnicodeObj = Dictionary.GetPageObject(PdfTokens.ToUnicodeKey);
            if (toUnicodeObj == null)
                return null;

            try
            {
                var cmapData = Document.StreamDecoder.DecodeContentStream(toUnicodeObj);
                var cMapContent = new PdfParseContext(cmapData);
                return PdfToUnicodeCMapParser.ParseCMapFromContext(ref cMapContent, Document);
            }
            catch (Exception)
            {
                // Silently handle parsing errors
                return null;
            }
        }
    }
}