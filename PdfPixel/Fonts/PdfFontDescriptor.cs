using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Fonts;

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
        AverageWidth = metrics.AvgWidth * GlyphSpaceUnitsPerEm;
        FontWeight = metrics.Weight;
        Panose = metrics.Panose;
        FontBoundingBox = new PdfRectangle(
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
        FontName = dictionary.GetString(PdfTokens.FontNameKey);
        Flags = (PdfFontFlags)dictionary.GetIntegerOrDefault(PdfTokens.FlagsKey);
        ItalicAngle = dictionary.GetFloatOrDefault(PdfTokens.ItalicAngleKey);
        Ascent = dictionary.GetFloatOrDefault(PdfTokens.AscentKey);
        Descent = dictionary.GetFloatOrDefault(PdfTokens.DescentKey);
        CapHeight = dictionary.GetFloatOrDefault(PdfTokens.CapHeightKey);
        XHeight = dictionary.GetFloatOrDefault(PdfTokens.XHeightKey);
        VerticalStemThickness = dictionary.GetFloatOrDefault(PdfTokens.StemVKey);
        HorizontalStemThickness = dictionary.GetFloatOrDefault(PdfTokens.StemHKey);
        AverageWidth = dictionary.GetFloatOrDefault(PdfTokens.AvgWidthKey);
        MaxWidth = dictionary.GetFloatOrDefault(PdfTokens.MaxWidthKey);
        MissingWidth = dictionary.GetFloatOrDefault(PdfTokens.MissingWidthKey);
        FontFamily = dictionary.GetString(PdfTokens.FontFamilyKey);
        FontStretch = dictionary.GetString(PdfTokens.FontStretchKey);
        FontWeight = dictionary.GetInteger(PdfTokens.FontWeightKey);
        Leading = dictionary.GetFloatOrDefault(PdfTokens.LeadingKey);
        CharacterSet = dictionary.GetString(PdfTokens.CharSetKey);

        // Optional arrays
        HorizontalStemSnapThicknesses = dictionary.GetArray(PdfTokens.StemSnapHKey)?.GetFloatArray();
        VerticalStemSnapThicknesses = dictionary.GetArray(PdfTokens.StemSnapVKey)?.GetFloatArray();

        // PANOSE (string or hex string)
        Panose = dictionary.GetValue(PdfTokens.PanoseKey).AsString()?.Value.ToArray();

        FontFile = PdfFontFile.GetFontFile(dictionary);

        // Parse FontBBox array
        FontBoundingBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.FontBBoxKey)) ?? PdfRectangle.Empty;
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
    public PdfRectangle FontBoundingBox { get; }

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
    public float VerticalStemThickness { get; }

    /// <summary>
    /// Horizontal stem thickness (/StemH).
    /// </summary>
    public float HorizontalStemThickness { get; }

    /// <summary>
    /// Average glyph width (/AvgWidth).
    /// </summary>
    public float AverageWidth { get; }

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
    public PdfString? CharacterSet { get; }

    /// <summary>
    /// Array of horizontal stem thicknesses (PDF 1.5+, /StemSnapH).
    /// </summary>
    public IReadOnlyList<float>? HorizontalStemSnapThicknesses { get; }

    /// <summary>
    /// Array of vertical stem thicknesses (PDF 1.5+, /StemSnapV).
    /// </summary>
    public IReadOnlyList<float>? VerticalStemSnapThicknesses { get; }

    /// <summary>
    /// PANOSE classification bytes (PDF 1.5+, /Panose).
    /// </summary>
    public byte[]? Panose { get; }

    /// <summary>
    /// The embedded font program (/FontFile, /FontFile2 or /FontFile3), or <see langword="null"/> when absent.
    /// </summary>
    public PdfFontFile? FontFile { get; }

    /// <summary>
    /// Gets a value indicating whether this font descriptor has any embedded font stream.
    /// </summary>
    public bool HasEmbeddedFont => FontFile != null;

    /// <summary>
    /// Returns the font descriptor held by <paramref name="key"/> of <paramref name="owner"/>, using the document's cache if available.
    /// </summary>
    /// <param name="owner">Dictionary holding the font descriptor entry.</param>
    /// <param name="key">Key of the font descriptor entry.</param>
    /// <returns>The font descriptor, or <see langword="null"/> when absent or not a dictionary.</returns>
    internal static PdfFontDescriptor? GetFontDescriptor(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    private static PdfFontDescriptor? FromObject(PdfObject descriptorObject)
    {
        PdfDictionary? dictionary = descriptorObject.Value.AsDictionary();
        if (dictionary == null)
        {
            return null;
        }

        return new PdfFontDescriptor(dictionary);
    }
}
