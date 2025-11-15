using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// Font file format enumeration
    /// Determined by embedded font stream and its dictionary (/FontFile, /FontFile2, /FontFile3 + /Subtype)
    /// </summary>
    [PdfEnum]
    public enum PdfFontFileFormat
    {
        /// <summary>
        /// Unknown or unspecified font file format.
        /// </summary>
        [PdfEnumDefaultValue]
        Unknown,

        /// <summary>
        /// Type 1 font program (PFA/PFB) (/Type1).
        /// </summary>
        [PdfEnumValue("Type1")]
        Type1,

        /// <summary>
        /// TrueType font program (/TrueType).
        /// </summary>
        [PdfEnumValue("TrueType")]
        TrueType,

        /// <summary>
        /// OpenType font program (SFNT wrapper) (/OpenType).
        /// </summary>
        [PdfEnumValue("OpenType")]
        OpenType,

        /// <summary>
        /// Compact Font Format (CFF) Type 1 font program (/Type1C).
        /// </summary>
        [PdfEnumValue("Type1C")]
        Type1C,

        /// <summary>
        /// CFF for CIDFonts (/CIDFontType0C).
        /// </summary>
        [PdfEnumValue("CIDFontType0C")]
        CIDFontType0C
    }

    // Font descriptor information
    public class PdfFontDescriptor
    {
        public PdfString FontName { get; set; }
        public PdfFontFlags Flags { get; set; }
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
        public PdfString FontFamily { get; set; }
        public PdfString FontStretch { get; set; }
        public int FontWeight { get; set; }
        public float Leading { get; set; }
        public PdfString CharSet { get; set; }
        public float[] StemSnapH { get; set; }
        public float[] StemSnapV { get; set; }
        public byte[] Panose { get; set; }
        /// <summary>
        /// The embedded font file object (only one exists at a time)
        /// </summary>
        public PdfObject FontFileObject { get; set; }

        /// <summary>
        /// Format of the embedded font file
        /// </summary>
        public PdfFontFileFormat FontFileFormat { get; set; }

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
                Flags = (PdfFontFlags)dict.GetIntegerOrDefault(PdfTokens.FlagsKey),
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
                var hexBytes = panoseVal.AsStringBytes();
                if (!hexBytes.IsEmpty)
                {
                    descriptor.Panose = hexBytes.ToArray();
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

        private static (PdfObject Object, PdfFontFileFormat Format) GetFileObjectAndFormat(PdfDictionary dict)
        {
            // Get font file object and determine format (only one exists at a time)
            // Priority order: FontFile2 (TrueType), FontFile3 (check /Subtype), FontFile (Type1)
            var fontFile2Obj = dict.GetObject(PdfTokens.FontFile2Key);
            if (fontFile2Obj != null)
            {
                return (fontFile2Obj, PdfFontFileFormat.TrueType);
            }

            var fontFile3Obj = dict.GetObject(PdfTokens.FontFile3Key);
            if (fontFile3Obj != null)
            {
                // For FontFile3 the actual program type is specified by the stream dictionary /Subtype
                var subType = fontFile3Obj.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontFileFormat>();
                return (fontFile3Obj, subType);
            }

            var fontFileObj = dict.GetObject(PdfTokens.FontFileKey);
            if (fontFileObj != null)
            {
                return (fontFileObj, PdfFontFileFormat.Type1);
            }

            return default;
        }
    }
}