using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Fonts;

/// <summary>
/// Factory for creating the correct font type based on PDF subtype
/// </summary>
public static class PdfFontFactory
{
    /// <summary>
    /// Determines whether a PdfDictionary is a font dictionary by inspecting its Type/Subtype.
    /// </summary>
    public static bool IsFont(PdfDictionary pdfDictionary)
    {
        if (pdfDictionary == null)
        {
            return false;
        }

        if (pdfDictionary.GetName(PdfTokens.TypeKey) == PdfTokens.FontKey)
        {
            return true;
        }

        PdfFontSubType subtype = pdfDictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();

        return subtype != PdfFontSubType.Unknown;
    }

    /// <summary>
    /// Returns the font held by <paramref name="key"/> of <paramref name="owner"/>, using the document's cache if available.
    /// </summary>
    /// <param name="owner">Dictionary holding the font entry.</param>
    /// <param name="key">Key of the font entry.</param>
    /// <returns>The font, or <see langword="null"/> when absent or not a supported font.</returns>
    internal static PdfFontBase? GetFont(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, CreateFont);

    /// <summary>
    /// Returns the font at <paramref name="index"/> of <paramref name="array"/>, using the document's cache if available.
    /// </summary>
    /// <param name="array">Array holding the font entry.</param>
    /// <param name="index">Index of the font entry.</param>
    /// <returns>The font, or <see langword="null"/> when absent or not a supported font.</returns>
    internal static PdfFontBase? GetFont(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, CreateFont);

    /// <summary>
    /// Creates a font object based on the specified PDF object.
    /// </summary>
    /// <remarks>The method determines the font subtype from the dictionary and returns an appropriate
    /// font object instance. Supported font subtypes include Type0, CIDFontType0, CIDFontType2,  Type1, TrueType,
    /// Type3, and MMType1.</remarks>
    /// <param name="fontObject">The PDF object containing font metadata and properties. Must represent a valid font
    /// object.</param>
    private static PdfFontBase? CreateFont(PdfObject? fontObject)
    {
        if (fontObject == null)
        {
            return null;
        }

        if (!IsFont(fontObject.Dictionary))
        {
            return null;
        }

        PdfFontSubType subtype = fontObject.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();

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
