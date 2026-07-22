using PdfPixel.Fonts;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.Models;
using System;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Intermediate base class for single-byte fonts (Simple fonts and Type3 fonts)
/// Provides common functionality for fonts limited to 256 characters with single-byte encoding.
/// </summary>
public abstract class PdfSingleByteFont : PdfFontBase
{
    /// <summary>
    /// Constructor for single-byte fonts - handles common initialization
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    protected PdfSingleByteFont(PdfObject fontObject)
        : base(fontObject)
    {
        Widths = PdfSingleByteFontWidths.Parse(fontObject.Dictionary);
        Encoding = PdfFontEncodingParser.ParseSingleByteEncoding(fontObject.Dictionary);
    }

    /// <summary>
    /// Encoding information for the font.
    /// </summary>
    public virtual PdfFontEncodingInfo Encoding { get; }

    /// <summary>
    /// Character width information
    /// Initialized during construction
    /// </summary>
    public PdfSingleByteFontWidths Widths { get; }

    /// <summary>
    /// Get character width from font metrics
    /// </summary>
    public override float GetWidth(PdfCharacterCode code)
    {
        float? width = Widths.GetWidth(code);
        if (width.HasValue)
        {
            return width.Value;
        }

        // Fallback: PDF spec recommends 0 if not defined for single-byte fonts
        return 0f;
    }

    /// <summary>
    /// Single-byte fonts do not support vertical writing; always returns the default (zero) metric.
    /// </summary>
    /// <param name="code">The character code (unused).</param>
    /// <returns>The default <see cref="PdfVerticalMetric"/>.</returns>
    public override PdfVerticalMetric GetVerticalDisplacement(PdfCharacterCode code) => default;

    /// <summary>
    /// Converts a character code to its Unicode string representation.
    /// First consults the ToUnicode CMap; if no mapping is found, falls back to the Adobe Glyph List via the font's encoding;
    /// if that also fails, falls back to treating the character code itself as a Unicode code point.
    /// </summary>
    /// <param name="code">The character code to convert.</param>
    /// <returns>The Unicode string for the character code, or <see langword="null"/> if no mapping is found.</returns>
    public override string? GetUnicodeString(PdfCharacterCode code)
    {
        string? baseResult = base.GetUnicodeString(code);

        if (baseResult != null)
        {
            return baseResult;
        }

        // Fallback to Adobe Glyph List mapping.
        PdfFontString name = Encoding.GetNameByCodeOrUndefined((byte)(uint)code);

        if (AdobeGlyphList.GetMap(Encoding.BaseEncoding.ToPdfFontEncoding()).TryGetValue(name, out string? aglUnicode))
        {
            return aglUnicode;
        }

        // Fallback: treat the character code itself as a Unicode code point.
        return char.ConvertFromUtf32((int)(uint)code);
    }

    /// <summary>
    /// Extracts character codes from raw bytes for single-byte fonts.
    /// Always uses single-byte segmentation.
    /// </summary>
    /// <param name="bytes">Raw bytes to extract character codes from.</param>
    /// <returns>Array of extracted PdfCharacterCode items.</returns>
    public override PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return Array.Empty<PdfCharacterCode>();
        }

        int count = bytes.Length;
        var result = new PdfCharacterCode[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = new PdfCharacterCode(bytes.Slice(index, 1));
        }

        return result;
    }
}
