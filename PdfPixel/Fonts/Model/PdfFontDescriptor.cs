using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Represents a PDF font descriptor, containing font metrics, style, and embedded font file information.
/// Provides access to font properties as defined in the PDF specification.
/// </summary>
public class PdfFontDescriptor
{
    /// <summary>
    /// The glyph space units one em is divided into, which every descriptor metric is stated in.
    /// </summary>
    private const float GlyphSpaceUnitsPerEm = 1000f;

    /// <summary>
    /// Initializes a descriptor from a typeface's own metrics, for a font whose dictionary states no
    /// descriptor of its own - which the specification allows a Standard 14 font to omit. Metrics arrive
    /// as fractions of the em square and are stated here in glyph space units (1/1000 em), as a
    /// descriptor read from a dictionary would be.
    /// </summary>
    /// <param name="metrics">The typeface's metrics.</param>
    /// <param name="isSymbolic">Whether the typeface addresses its glyphs through a built-in symbol encoding.</param>
    internal PdfFontDescriptor(PdfFontMetrics metrics, bool isSymbolic)
    {
        PdfFontFlags flags = isSymbolic ? PdfFontFlags.Symbolic : PdfFontFlags.Nonsymbolic;

        if (metrics.IsItalic)
        {
            flags |= PdfFontFlags.Italic;
        }

        if (metrics.IsForceBold)
        {
            flags |= PdfFontFlags.ForceBold;
        }

        FontName = metrics.FontName.ToPdfString();
        FontFamily = metrics.FamilyName.ToPdfString();
        Flags = flags;
        ItalicAngle = metrics.ItalicAngle;
        Ascent = metrics.Ascent * GlyphSpaceUnitsPerEm;
        Descent = metrics.Descent * GlyphSpaceUnitsPerEm;
        CapHeight = metrics.CapHeight * GlyphSpaceUnitsPerEm;
        XHeight = metrics.XHeight * GlyphSpaceUnitsPerEm;
        AvgWidth = metrics.AvgWidth * GlyphSpaceUnitsPerEm;
        FontWeight = metrics.Weight;
        Panose = metrics.Panose;
        FontBBox = new PdfRectangle(
            metrics.BoundingBoxLeft * GlyphSpaceUnitsPerEm,
            metrics.BoundingBoxBottom * GlyphSpaceUnitsPerEm,
            metrics.BoundingBoxRight * GlyphSpaceUnitsPerEm,
            metrics.BoundingBoxTop * GlyphSpaceUnitsPerEm);
    }

    /// <summary>
    /// Reads the font descriptor <paramref name="dictionary"/>.
    /// </summary>
    /// <param name="dictionary">The font descriptor dictionary.</param>
    internal PdfFontDescriptor(PdfDictionary dictionary)
    {
        ObjectCache = dictionary.Document.ObjectCache;
        FontName = dictionary.GetString(PdfTokens.FontNameKey);
        Flags = (PdfFontFlags)dictionary.GetIntegerOrDefault(PdfTokens.FlagsKey);
        ItalicAngle = dictionary.GetFloatOrDefault(PdfTokens.ItalicAngleKey);
        Ascent = dictionary.GetFloatOrDefault(PdfTokens.AscentKey);
        Descent = dictionary.GetFloatOrDefault(PdfTokens.DescentKey);
        CapHeight = dictionary.GetFloatOrDefault(PdfTokens.CapHeightKey);
        XHeight = dictionary.GetFloatOrDefault(PdfTokens.XHeightKey);
        StemV = dictionary.GetFloatOrDefault(PdfTokens.StemVKey);
        StemH = dictionary.GetFloatOrDefault(PdfTokens.StemHKey);
        AvgWidth = dictionary.GetFloatOrDefault(PdfTokens.AvgWidthKey);
        MaxWidth = dictionary.GetFloatOrDefault(PdfTokens.MaxWidthKey);
        MissingWidth = dictionary.GetFloatOrDefault(PdfTokens.MissingWidthKey);
        FontFamily = dictionary.GetString(PdfTokens.FontFamilyKey);
        FontStretch = dictionary.GetString(PdfTokens.FontStretchKey);
        FontWeight = dictionary.GetInteger(PdfTokens.FontWeightKey);
        Leading = dictionary.GetFloatOrDefault(PdfTokens.LeadingKey);
        CharSet = dictionary.GetString(PdfTokens.CharSetKey);

        // Optional arrays
        StemSnapH = dictionary.GetArray(PdfTokens.StemSnapHKey)?.GetFloatArray();
        StemSnapV = dictionary.GetArray(PdfTokens.StemSnapVKey)?.GetFloatArray();

        // PANOSE (string or hex string)
        Panose = dictionary.GetValue(PdfTokens.PanoseKey).AsString()?.Value.ToArray();

        (PdfObject? fontFileObject, PdfFontFileFormat format) = GetFileObjectAndFormat(dictionary);
        FontFileFormat = format;
        if (fontFileObject != null)
        {
            FontFileStream = fontFileObject.Stream;
            FontFileReference = fontFileObject.Reference;
            FontFileLength1 = fontFileObject.Dictionary.GetIntegerOrDefault(PdfTokens.Length1);
            FontFileLength2 = fontFileObject.Dictionary.GetIntegerOrDefault(PdfTokens.Length2);
            FontFileLength3 = fontFileObject.Dictionary.GetIntegerOrDefault(PdfTokens.Length3);
        }

        // Parse FontBBox array
        FontBBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.FontBBoxKey)) ?? PdfRectangle.Empty;
    }

    /// <summary>
    /// The font name as specified in the PDF dictionary (/FontName).
    /// </summary>
    public PdfString? FontName { get; }

    /// <summary>
    /// Flags describing font characteristics (see PDF spec Table 5.19).
    /// </summary>
    public PdfFontFlags Flags { get; }

    /// <summary>
    /// The font bounding box (/FontBBox) in glyph design units.
    /// </summary>
    public PdfRectangle FontBBox { get; }

    /// <summary>
    /// Italic angle of the font (/ItalicAngle).
    /// </summary>
    public float ItalicAngle { get; }

    /// <summary>
    /// Maximum height above baseline for glyphs in font (/Ascent).
    /// </summary>
    public float Ascent { get; }

    /// <summary>
    /// Maximum depth below baseline for glyphs in font (/Descent).
    /// </summary>
    public float Descent { get; }

    /// <summary>
    /// Height of uppercase glyphs (/CapHeight).
    /// </summary>
    public float CapHeight { get; }

    /// <summary>
    /// Height of lowercase x glyph (/XHeight).
    /// </summary>
    public float XHeight { get; }

    /// <summary>
    /// Vertical stem thickness (/StemV).
    /// </summary>
    public float StemV { get; }

    /// <summary>
    /// Horizontal stem thickness (/StemH).
    /// </summary>
    public float StemH { get; }

    /// <summary>
    /// Average glyph width (/AvgWidth).
    /// </summary>
    public float AvgWidth { get; }

    /// <summary>
    /// Maximum glyph width (/MaxWidth).
    /// </summary>
    public float MaxWidth { get; }

    /// <summary>
    /// Width to use for missing glyphs (/MissingWidth).
    /// </summary>
    public float MissingWidth { get; }

    /// <summary>
    /// Font family name (PDF 1.5+, /FontFamily).
    /// </summary>
    public PdfString? FontFamily { get; }

    /// <summary>
    /// Font stretch value (PDF 1.5+, /FontStretch).
    /// </summary>
    public PdfString? FontStretch { get; }

    /// <summary>
    /// Font weight (PDF 1.5+, /FontWeight).
    /// </summary>
    public int? FontWeight { get; }

    /// <summary>
    /// Leading value (PDF 1.5+, /Leading).
    /// </summary>
    public float Leading { get; }

    /// <summary>
    /// Character set description (PDF 1.5+, /CharSet).
    /// </summary>
    public PdfString? CharSet { get; }

    /// <summary>
    /// Array of horizontal stem thicknesses (PDF 1.5+, /StemSnapH).
    /// </summary>
    public IReadOnlyList<float>? StemSnapH { get; }

    /// <summary>
    /// Array of vertical stem thicknesses (PDF 1.5+, /StemSnapV).
    /// </summary>
    public IReadOnlyList<float>? StemSnapV { get; }

    /// <summary>
    /// PANOSE classification bytes (PDF 1.5+, /Panose).
    /// </summary>
    public byte[]? Panose { get; }

    /// <summary>
    /// The embedded font file stream (only one exists at a time).
    /// </summary>
    public PdfObjectStream? FontFileStream { get; }

    /// <summary>
    /// Reference of the object holding <see cref="FontFileStream"/>, which several descriptors may
    /// share. <see langword="null"/> when this descriptor embeds no font program.
    /// </summary>
    public PdfReference? FontFileReference { get; }

    /// <summary>
    /// Format of the embedded font file.
    /// </summary>
    public PdfFontFileFormat FontFileFormat { get; }

    /// <summary>
    /// Type1 font stream segment length for the clear-text portion (/Length1).
    /// </summary>
    public int FontFileLength1 { get; }

    /// <summary>
    /// Type1 font stream segment length for the binary portion (/Length2).
    /// </summary>
    public int FontFileLength2 { get; }

    /// <summary>
    /// Type1 font stream segment length for the fixed-content trailer (/Length3).
    /// </summary>
    public int FontFileLength3 { get; }

    /// <summary>
    /// Object cache of the document this descriptor was created from, used to share one parsed typeface
    /// between every descriptor embedding the same font program.
    /// </summary>
    internal PdfDocumentObjectCache? ObjectCache { get; }

    /// <summary>
    /// Gets a value indicating whether this font descriptor has any embedded font stream.
    /// </summary>
    public bool HasEmbeddedFont => FontFileStream != null;

    /// <summary>
    /// Gets the embedded font file object and its format from the font descriptor dictionary.
    /// </summary>
    /// <param name="dict">The PDF font descriptor dictionary.</param>
    /// <returns>A tuple containing the font file object and its format.</returns>
    private static (PdfObject Object, PdfFontFileFormat Format) GetFileObjectAndFormat(PdfDictionary dict)
    {
        // Get font file object and determine format (only one exists at a time)
        // Priority order: FontFile2 (TrueType), FontFile3 (check /Subtype), FontFile (Type1)
        PdfObject? fontFile2Obj = dict.GetObject(PdfTokens.FontFile2Key);
        if (fontFile2Obj != null)
        {
            return (fontFile2Obj, PdfFontFileFormat.TrueType);
        }

        PdfObject? fontFile3Obj = dict.GetObject(PdfTokens.FontFile3Key);
        if (fontFile3Obj != null)
        {
            // For FontFile3 the actual program type is specified by the stream dictionary /Subtype
            PdfFontFileFormat subType = fontFile3Obj.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfFontFileFormat>();
            return (fontFile3Obj, subType);
        }

        PdfObject? fontFileObj = dict.GetObject(PdfTokens.FontFileKey);
        if (fontFileObj != null)
        {
            return (fontFileObj, PdfFontFileFormat.Type1);
        }

        return default;
    }
}
