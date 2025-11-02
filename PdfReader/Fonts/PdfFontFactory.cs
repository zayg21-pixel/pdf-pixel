using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Factory for creating the correct font type based on PDF subtype
    /// </summary>
    public static class PdfFontFactory
    {
        /// <summary>
        /// Determine if a PdfDictionary is a font dictionary by inspecting its Type/Subtype.
        /// </summary>
        public static bool IsFont(PdfDictionary pdfDictionary)
        {
            if (pdfDictionary == null)
            {
                return false;
            }

            var type = pdfDictionary.GetName(PdfTokens.TypeKey);

            if (type == PdfTokens.FontKey)
            {
                return true;
            }

            var subtype = pdfDictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();

            return subtype != PdfFontSubType.Unknown;
        }

        /// <summary>
        /// Creates a font object based on the specified PDF dictionary.
        /// </summary>
        /// <remarks>The method determines the font subtype from the dictionary and returns an appropriate
        /// font object instance. Supported font subtypes include Type0, CIDFontType0, CIDFontType2,  Type1, TrueType,
        /// Type3, and MMType1. If the subtype is unrecognized, a simple font object  is returned as a
        /// fallback.</remarks>
        /// <param name="dictionary">The PDF dictionary containing font metadata and properties.  This dictionary must represent a valid font
        /// object.</param>
        public static PdfFontBase CreateFont(PdfDictionary dictionary)
        {
            if (!IsFont(dictionary))
            {
                return null;
            }

            var subtype = dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();

            return subtype switch
            {
                PdfFontSubType.Type0 => new PdfCompositeFont(dictionary),
                PdfFontSubType.CidFontType0 => new PdfCIDFont(dictionary),
                PdfFontSubType.CidFontType2 => new PdfCIDFont(dictionary),
                PdfFontSubType.Type1 => new PdfSimpleFont(dictionary),
                PdfFontSubType.TrueType => new PdfSimpleFont(dictionary),
                PdfFontSubType.Type3 => new PdfType3Font(dictionary),
                PdfFontSubType.MMType1 => new PdfSimpleFont(dictionary),
                _ => new PdfSimpleFont(dictionary) // Fallback for unknown subtypes under /Font
            };
        }
    }
}