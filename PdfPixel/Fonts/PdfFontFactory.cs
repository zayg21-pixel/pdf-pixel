using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts;

/// <summary>
/// Factory for creating the correct font type based on PDF subtype
/// </summary>
public static class PdfFontFactory
{
    /// <summary>
    /// Create a font object for text shaping and measurement.
    /// </summary>
    /// <param name="typeface">Typeface to use</param>
    /// <returns>Configured SKFont</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKFont CreateTextFont(SKTypeface typeface)
    {
        SKFont font = new()
        {
            Typeface = typeface,
            Size = 1,
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.Slight
        };

        // Skew/rotation are already represented in the text matrix applied at draw time.
        return font;
    }

    /// <summary>
    /// Creates a deep copy of the given font, preserving all properties.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKFont CloneFont(SKFont font)
    {
        return new()
        {
            Typeface = font.Typeface,
            BaselineSnap = font.BaselineSnap,
            Edging = font.Edging,
            EmbeddedBitmaps = font.EmbeddedBitmaps,
            Embolden = font.Embolden,
            ForceAutoHinting = font.ForceAutoHinting,
            Hinting = font.Hinting,
            LinearMetrics = font.LinearMetrics,
            ScaleX = font.ScaleX,
            Size = font.Size,
            Subpixel = font.Subpixel,
            SkewX = font.SkewX
        };
    }

    /// <summary>
    /// Determine if a PdfDictionary is a font dictionary by inspecting its Type/Subtype.
    /// </summary>
    public static bool IsFont(PdfDictionary pdfDictionary)
    {
        if (pdfDictionary == null)
        {
            return false;
        }

        PdfString type = pdfDictionary.GetName(PdfTokens.TypeKey);

        if (type == PdfTokens.FontKey)
        {
            return true;
        }

        PdfFontSubType subtype = pdfDictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();

        return subtype != PdfFontSubType.Unknown;
    }

    /// <summary>
    /// Creates a font object based on the specified PDF object.
    /// </summary>
    /// <remarks>The method determines the font subtype from the dictionary and returns an appropriate
    /// font object instance. Supported font subtypes include Type0, CIDFontType0, CIDFontType2,  Type1, TrueType,
    /// Type3, and MMType1.</remarks>
    /// <param name="fontObject">The PDF object containing font metadata and properties. Must represent a valid font
    /// object.</param>
    internal static PdfFontBase? CreateFont(PdfObject? fontObject)
    {
        if (fontObject == null)
        {
            return null;
        }

        if (!IsFont(fontObject.Dictionary))
        {
            return null;
        }

        PdfFontSubType subtype = fontObject.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();

        return subtype switch
        {
            PdfFontSubType.Type0 => new PdfCompositeFont(fontObject),
            PdfFontSubType.CidFontType0 => new PdfCidFont(fontObject),
            PdfFontSubType.CidFontType2 => new PdfCidFont(fontObject),
            PdfFontSubType.Type1 => new PdfSimpleFont(fontObject),
            PdfFontSubType.TrueType => new PdfSimpleFont(fontObject),
            PdfFontSubType.Type3 => new PdfType3Font(fontObject),
            PdfFontSubType.MMType1 => new PdfSimpleFont(fontObject),
            _ => null
        };
    }
}
