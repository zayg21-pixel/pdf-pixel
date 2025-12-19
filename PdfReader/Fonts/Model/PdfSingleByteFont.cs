using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Intermediate base class for single-byte fonts (Simple fonts and Type3 fonts)
/// Provides common functionality for fonts limited to 256 characters with single-byte encoding
/// Uses thread-safe Lazy&lt;T&gt; pattern for heavy operations
/// </summary>
public abstract class PdfSingleByteFont : PdfFontBase
{
    /// <summary>
    /// Constructor for single-byte fonts - handles common initialization
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    public PdfSingleByteFont(PdfObject fontObject) : base(fontObject)
    {
        Widths = SingleByteFontWidths.Parse(fontObject.Dictionary);
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
    public SingleByteFontWidths Widths { get; }

    /// <summary>
    /// Get character width from font metrics
    /// </summary>
    public override float GetWidth(PdfCharacterCode code)
    {
        var width = Widths.GetWidth(code);
        if (width.HasValue)
        {
            return width.Value;
        }
        // Fallback: PDF spec recommends 0 if not defined for single-byte fonts
        return 0f;
    }

    public override VerticalMetric GetVerticalDisplacement(PdfCharacterCode code)
    {
        return default;
    }

    public override string GetUnicodeString(PdfCharacterCode code)
    {
        var baseResult = base.GetUnicodeString(code);

        if (baseResult != null)
        {
            return baseResult;
        }

        // Fallback to Adobe Glyph List mapping.
        PdfString name = SingleByteEncodings.GetNameByCodeOrUndefined((byte)(uint)code, Encoding.BaseEncoding, Encoding.Differences);

        if (AdobeGlyphList.CharacterMap.TryGetValue(name, out var aglUnicode))
        {
            return aglUnicode;
        }

        return null;
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