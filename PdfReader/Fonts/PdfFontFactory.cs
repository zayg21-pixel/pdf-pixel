using PdfReader.Models;
using PdfReader.Fonts.Types;

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
            var subtype = pdfDictionary.GetName(PdfTokens.SubtypeKey);

            if (type == PdfTokens.FontKey)
                return true;

            // Some font dictionaries (descendant CID fonts, etc.) rely on subtype
            switch (subtype)
            {
                case PdfTokens.Type0FontKey:
                case PdfTokens.CIDFontType0Key:
                case PdfTokens.CIDFontType2Key:
                case PdfTokens.Type1FontKey:
                case PdfTokens.TrueTypeFontKey:
                case PdfTokens.Type3FontKey:
                case PdfTokens.MMType1FontKey:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Create appropriate font type based on PDF object
        /// Returns the correct subclass: PdfSimpleFont, PdfCIDFont, PdfCompositeFont, or PdfType3Font
        /// </summary>
        public static PdfFontBase CreateFont(PdfObject fontObject)
        {
            if (fontObject == null)
            {
                return null;
            }

            return CreateFont(fontObject.Dictionary);
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

            var subtype = dictionary.GetName(PdfTokens.SubtypeKey);

            return subtype switch
            {
                PdfTokens.Type0FontKey => new PdfCompositeFont(dictionary),
                PdfTokens.CIDFontType0Key => new PdfCIDFont(dictionary),
                PdfTokens.CIDFontType2Key => new PdfCIDFont(dictionary),
                PdfTokens.Type1FontKey => new PdfSimpleFont(dictionary),
                PdfTokens.TrueTypeFontKey => new PdfSimpleFont(dictionary),
                PdfTokens.Type3FontKey => new PdfType3Font(dictionary),
                PdfTokens.MMType1FontKey => new PdfSimpleFont(dictionary),
                _ => new PdfSimpleFont(dictionary) // Fallback for unknown subtypes under /Font
            };
        }
    }
}