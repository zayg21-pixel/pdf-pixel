using PdfReader.Models;
using PdfReader.Rendering.Operators;
using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Fonts.Model;

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
/// <summary>
/// Represents a PDF font descriptor, containing font metrics, style, and embedded font file information.
/// Provides access to font properties as defined in the PDF specification.
/// </summary>
public class PdfFontDescriptor
{
    /// <summary>
    /// The font name as specified in the PDF dictionary (/FontName).
    /// </summary>
    public PdfString FontName { get; set; }

    /// <summary>
    /// Flags describing font characteristics (see PDF spec Table 5.19).
    /// </summary>
    public PdfFontFlags Flags { get; set; }

    /// <summary>
    /// The font bounding box (/FontBBox) in glyph design units.
    /// </summary>
    public SKRect FontBBox { get; set; }

    /// <summary>
    /// Italic angle of the font (/ItalicAngle).
    /// </summary>
    public float ItalicAngle { get; set; }

    /// <summary>
    /// Maximum height above baseline for glyphs in font (/Ascent).
    /// </summary>
    public float Ascent { get; set; }

    /// <summary>
    /// Maximum depth below baseline for glyphs in font (/Descent).
    /// </summary>
    public float Descent { get; set; }

    /// <summary>
    /// Height of uppercase glyphs (/CapHeight).
    /// </summary>
    public float CapHeight { get; set; }

    /// <summary>
    /// Height of lowercase x glyph (/XHeight).
    /// </summary>
    public float XHeight { get; set; }

    /// <summary>
    /// Vertical stem thickness (/StemV).
    /// </summary>
    public float StemV { get; set; }

    /// <summary>
    /// Horizontal stem thickness (/StemH).
    /// </summary>
    public float StemH { get; set; }

    /// <summary>
    /// Average glyph width (/AvgWidth).
    /// </summary>
    public float AvgWidth { get; set; }

    /// <summary>
    /// Maximum glyph width (/MaxWidth).
    /// </summary>
    public float MaxWidth { get; set; }

    /// <summary>
    /// Width to use for missing glyphs (/MissingWidth).
    /// </summary>
    public float MissingWidth { get; set; }

    /// <summary>
    /// Font family name (PDF 1.5+, /FontFamily).
    /// </summary>
    public PdfString FontFamily { get; set; }

    /// <summary>
    /// Font stretch value (PDF 1.5+, /FontStretch).
    /// </summary>
    public PdfString FontStretch { get; set; }

    /// <summary>
    /// Font weight (PDF 1.5+, /FontWeight).
    /// </summary>
    public int FontWeight { get; set; }

    /// <summary>
    /// Leading value (PDF 1.5+, /Leading).
    /// </summary>
    public float Leading { get; set; }

    /// <summary>
    /// Character set description (PDF 1.5+, /CharSet).
    /// </summary>
    public PdfString CharSet { get; set; }

    /// <summary>
    /// Array of horizontal stem thicknesses (PDF 1.5+, /StemSnapH).
    /// </summary>
    public float[] StemSnapH { get; set; }

    /// <summary>
    /// Array of vertical stem thicknesses (PDF 1.5+, /StemSnapV).
    /// </summary>
    public float[] StemSnapV { get; set; }

    /// <summary>
    /// PANOSE classification bytes (PDF 1.5+, /Panose).
    /// </summary>
    public byte[] Panose { get; set; }

    /// <summary>
    /// The embedded font file object (only one exists at a time).
    /// </summary>
    public PdfObject FontFileObject { get; set; }

    /// <summary>
    /// Format of the embedded font file.
    /// </summary>
    public PdfFontFileFormat FontFileFormat { get; set; }

    /// <summary>
    /// Reference to the original dictionary this descriptor was created from.
    /// </summary>
    public PdfDictionary Dictionary { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this font descriptor has any embedded font stream.
    /// </summary>
    public bool HasEmbeddedFont => FontFileObject != null;

    /// <summary>
    /// Creates a <see cref="PdfFontDescriptor"/> from a PDF font descriptor dictionary.
    /// </summary>
    /// <param name="dict">The PDF dictionary containing font descriptor properties.</param>
    /// <returns>A populated <see cref="PdfFontDescriptor"/> instance, or null if <paramref name="dict"/> is null.</returns>
    public static PdfFontDescriptor FromDictionary(PdfDictionary dict)
    {
        if (dict == null)
        {
            return null;
        }

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
        descriptor.FontBBox = PdfLocationUtilities.CreateBBox(dict.GetArray(PdfTokens.FontBBoxKey)) ?? SKRect.Empty;

        return descriptor;
    }

    /// <summary>
    /// Gets the embedded font file object and its format from the font descriptor dictionary.
    /// </summary>
    /// <param name="dict">The PDF font descriptor dictionary.</param>
    /// <returns>A tuple containing the font file object and its format.</returns>
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