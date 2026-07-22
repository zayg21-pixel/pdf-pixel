using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a parsed CFF Top DICT.
/// </summary>
public class CffTopDict
{
    /// <summary>
    /// Gets or sets the SID of the font's weight string (e.g. "Bold", "Regular").
    /// </summary>
    public int? Weight { get; set; }

    /// <summary>
    /// Gets or sets the charstring format (1 = Type1, 2 = Type2).
    /// </summary>
    public int? CharstringType { get; set; }

    /// <summary>
    /// Gets or sets the font matrix mapping character space to text space.
    /// </summary>
    public PdfFontMatrix? FontMatrix { get; set; }

    /// <summary>
    /// Gets or sets the font bounding box (minX, minY, maxX, maxY). Feeds PdfFontMetrics.
    /// </summary>
    public float[]? FontBBox { get; set; }

    /// <summary>
    /// Gets or sets the italic angle in degrees counter-clockwise from vertical. Feeds PdfFontMetrics.
    /// </summary>
    public float? ItalicAngle { get; set; }

    /// <summary>
    /// Gets or sets the paint type (0 = filled, 2 = stroked).
    /// </summary>
    public int? PaintType { get; set; }

    /// <summary>
    /// Gets or sets the stroke width, used when <see cref="PaintType"/> is 2.
    /// </summary>
    public float? StrokeWidth { get; set; }

    // Offsets to other structures within the CFF data

    /// <summary>
    /// Gets or sets the charset offset, or a predefined charset ID (0, 1, 2).
    /// </summary>
    public int? CharsetOffset { get; set; }

    /// <summary>
    /// Gets or sets the encoding offset, or a predefined encoding ID (0, 1).
    /// </summary>
    public int? EncodingOffset { get; set; }

    /// <summary>
    /// Gets or sets the CharStrings INDEX offset.
    /// </summary>
    public int? CharStringsOffset { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes of the Private DICT.
    /// </summary>
    public int? PrivateDictSize { get; set; }

    /// <summary>
    /// Gets or sets the offset of the Private DICT.
    /// </summary>
    public int? PrivateDictOffset { get; set; }

    /// <summary>
    /// Gets or sets the ROS (Registry-Ordering-Supplement) operator. Present only for CID-keyed fonts;
    /// its presence is what identifies the font as CID-keyed.
    /// </summary>
    public CffRos? Ros { get; set; }

    /// <summary>
    /// Gets or sets the FDArray (Font DICT INDEX) offset. Present only for CID-keyed fonts.
    /// </summary>
    public int? FdArrayOffset { get; set; }

    /// <summary>
    /// Gets or sets the FDSelect (glyph-to-Font-DICT mapping) offset. Present only for CID-keyed fonts.
    /// </summary>
    public int? FdSelectOffset { get; set; }

    /// <summary>
    /// Gets or sets every DICT entry not individually modeled above -- unparsed/unknown to this reader
    /// (descriptive strings, unique IDs, CID metadata, synthetic-font operators, etc.) -- preserved
    /// verbatim so they can be written back unchanged.
    /// </summary>
    public List<CffDictEntry> Entries { get; set; } = [];

    /// <summary>
    /// Combines <paramref name="topDict"/>'s FontMatrix with <paramref name="fdTopDict"/>'s (if given),
    /// applying <paramref name="fdTopDict"/>'s matrix first, then <paramref name="topDict"/>'s. A
    /// missing <paramref name="topDict"/> FontMatrix combines as identity when <paramref name="fdTopDict"/>
    /// has its own; otherwise as the ordinary CFF default.
    /// </summary>
    public static PdfFontMatrix CombineFontMatrix(CffTopDict topDict, CffTopDict? fdTopDict)
    {
        if (topDict == null)
        {
            throw new ArgumentNullException(nameof(topDict));
        }

        PdfFontMatrix? topFontMatrix = topDict.FontMatrix;
        PdfFontMatrix? fdFontMatrix = fdTopDict?.FontMatrix;

        if (!topFontMatrix.HasValue && !fdFontMatrix.HasValue)
        {
            return PdfFontMatrix.Default;
        }

        PdfFontMatrix effectiveTopMatrix = topFontMatrix ?? PdfFontMatrix.Identity;
        return effectiveTopMatrix.PreConcat(fdFontMatrix ?? PdfFontMatrix.Identity);
    }
}
