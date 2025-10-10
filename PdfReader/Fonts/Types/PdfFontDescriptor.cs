using System;
using PdfReader.Fonts.Cff;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Font file format enumeration
    /// Determined by embedded font stream and its dictionary (/FontFile, /FontFile2, /FontFile3 + /Subtype)
    /// </summary>
    public enum FontFileFormat
    {
        Unknown,
        Type1,        // FontFile - Type 1 font program (PFA/PFB)
        TrueType,     // FontFile2 - TrueType font program
        OpenType,     // FontFile3 with /Subtype /OpenType (SFNT wrapper)
        Type1C,       // FontFile3 with /Subtype /Type1C (CFF compact)
        CIDFontType0C // FontFile3 with /Subtype /CIDFontType0C (CFF for CIDFonts)
    }

    // Font descriptor information
    public class PdfFontDescriptor
    {
        public string FontName { get; set; }
        public CffFontFlags Flags { get; set; }
        public SKRect FontBBox { get; set; }
        public float ItalicAngle { get; set; }
        public float Ascent { get; set; }
        public float Descent { get; set; }
        public float CapHeight { get; set; }
        public float XHeight { get; set; }
        public float StemV { get; set; }
        public float StemH { get; set; }
        public float AvgWidth { get; set; }
        public float MaxWidth { get; set; }
        public float MissingWidth { get; set; }
        // Extended properties (PDF 1.5+)
        public string FontFamily { get; set; }
        public string FontStretch { get; set; }
        public int FontWeight { get; set; }
        public float Leading { get; set; }
        public string CharSet { get; set; }
        public float[] StemSnapH { get; set; }
        public float[] StemSnapV { get; set; }
        public bool IsCffFont => FontFileFormat == FontFileFormat.Type1C || FontFileFormat == FontFileFormat.CIDFontType0C;
        public byte[] Panose { get; set; }
        /// <summary>
        /// The embedded font file object (only one exists at a time)
        /// </summary>
        public PdfObject FontFileObject { get; set; }

        /// <summary>
        /// Format of the embedded font file
        /// </summary>
        public FontFileFormat FontFileFormat { get; set; }

        /// <summary>
        /// Reference to the original dictionary this descriptor was created from
        /// </summary>
        public PdfDictionary Dictionary { get; private set; }

        /// <summary>
        /// Check if this font descriptor has any embedded font stream
        /// </summary>
        public bool HasEmbeddedFont => FontFileObject != null;

        public static PdfFontDescriptor FromDictionary(PdfDictionary dict)
        {
            var descriptor = new PdfFontDescriptor
            {
                Dictionary = dict, // Store reference to the dictionary
                FontName = dict.GetString(PdfTokens.FontNameKey),
                Flags = (CffFontFlags)dict.GetIntegerOrDefault(PdfTokens.FlagsKey),
                ItalicAngle = dict.GetFloatOrDefault(PdfTokens.ItalicAngleKey),
                Ascent = dict.GetFloatOrDefault(PdfTokens.AscentKey),
                Descent = dict.GetFloatOrDefault(PdfTokens.DescentKey),
                CapHeight = dict.GetFloatOrDefault(PdfTokens.CapHeightKey),
                XHeight = dict.GetFloatOrDefault(PdfTokens.XHeightKey),
                StemV = dict.GetFloatOrDefault(PdfTokens.StemVKey),
                StemH = dict.GetFloatOrDefault(PdfTokens.StemHKey),
                AvgWidth = dict.GetFloatOrDefault(PdfTokens.AvgWidthKey),
                MaxWidth = dict.GetFloatOrDefault(PdfTokens.MaxWidthKey),
                MissingWidth = dict.GetFloatOrDefault(PdfTokens.MissingWidthKey),
                FontFamily = dict.GetString(PdfTokens.FontFamilyKey),
                FontStretch = dict.GetString(PdfTokens.FontStretchKey),
                FontWeight = dict.GetIntegerOrDefault(PdfTokens.FontWeightKey),
                Leading = dict.GetFloatOrDefault(PdfTokens.LeadingKey),
                CharSet = dict.GetString(PdfTokens.CharSetKey)
            };

            // Optional arrays
            descriptor.StemSnapH = dict.GetArray(PdfTokens.StemSnapHKey).GetFloatArray();

            descriptor.StemSnapV = dict.GetArray(PdfTokens.StemSnapVKey).GetFloatArray();

            // PANOSE (string or hex string)
            var panoseVal = dict.GetValue(PdfTokens.PanoseKey);
            if (panoseVal != null)
            {
                var hexBytes = panoseVal.AsHexBytes();
                if (hexBytes != null)
                {
                    descriptor.Panose = hexBytes;
                }
                else
                {
                    var str = panoseVal.AsString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        descriptor.Panose = System.Text.Encoding.ASCII.GetBytes(str);
                    }
                }
            }

            var objectAndFormat = GetFileObjectAndFormat(dict);
            descriptor.FontFileObject = objectAndFormat.Object;
            descriptor.FontFileFormat = objectAndFormat.Format;

            // Parse FontBBox array
            var fontBBoxArray = dict.GetArray(PdfTokens.FontBBoxKey).GetFloatArray();
            if (fontBBoxArray?.Length >= 4)
            {
                descriptor.FontBBox = new SKRect(fontBBoxArray[0], fontBBoxArray[1], fontBBoxArray[2], fontBBoxArray[3]);
            }

            return descriptor;
        }

        private static (PdfObject Object, FontFileFormat Format) GetFileObjectAndFormat(PdfDictionary dict)
        {
            // Get font file object and determine format (only one exists at a time)
            // Priority order: FontFile2 (TrueType), FontFile3 (check /Subtype), FontFile (Type1)
            var fontFile2Obj = dict.GetPageObject(PdfTokens.FontFile2Key);
            if (fontFile2Obj != null)
            {
                return (fontFile2Obj, FontFileFormat.TrueType);
            }

            var fontFile3Obj = dict.GetPageObject(PdfTokens.FontFile3Key);
            if (fontFile3Obj != null)
            {
                // For FontFile3 the actual program type is specified by the stream dictionary /Subtype (Name with leading '/')
                var subType = fontFile3Obj.Dictionary?.GetName(PdfTokens.SubtypeKey);
                switch (subType)
                {
                    case "/Type1C":
                        return (fontFile3Obj, FontFileFormat.Type1C);
                    case "/CIDFontType0C":
                        return (fontFile3Obj, FontFileFormat.CIDFontType0C);
                    case "/OpenType":
                        return (fontFile3Obj, FontFileFormat.OpenType);
                    default:
                        // Unknown/unspecified subtype for FontFile3; assume OpenType wrapper
                        return (fontFile3Obj, FontFileFormat.OpenType);
                }
            }

            var fontFileObj = dict.GetPageObject(PdfTokens.FontFileKey);
            if (fontFileObj != null)
            {
                return (fontFileObj, FontFileFormat.Type1);
            }

            return default;
        }

        /// <summary>
        /// Get font stream data, either from cache or by decoding and caching
        /// Returns the decoded font stream data
        /// </summary>
        public ReadOnlyMemory<byte> GetFontStream()
        {
            if (FontFileObject == null)
                return ReadOnlyMemory<byte>.Empty;

            var document = Dictionary.Document;
            var fontCache = document.FontCache;

            // Get the font stream data using the font file object
            return fontCache.GetFontStream(this);
        }

        internal CffNameKeyedInfo GetCffInfo()
        {
            var document = Dictionary.Document;
            var fontCache = document.FontCache;

            // Get the font stream data using the font file object
            return fontCache.GetCffInfo(this);
        }
    }
}