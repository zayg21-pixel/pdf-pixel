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
        /// Determine if a PdfObject is a font object by inspecting its dictionary Type/Subtype.
        /// </summary>
        public static bool IsFontObject(PdfObject obj)
        {
            if (obj == null) return false;
            var dict = obj.Dictionary;
            if (dict == null) return false;

            var type = dict.GetName(PdfTokens.TypeKey);
            var subtype = dict.GetName(PdfTokens.SubtypeKey);

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
            var dict = fontObject.Dictionary;

            if (dict == null)
            {
                return null;
            }

            if (!IsFontObject(fontObject))
            {
                return null;
            }

            var subtype = dict.GetName(PdfTokens.SubtypeKey);

            return subtype switch
            {
                PdfTokens.Type0FontKey => new PdfCompositeFont(fontObject),
                PdfTokens.CIDFontType0Key => new PdfCIDFont(fontObject),
                PdfTokens.CIDFontType2Key => new PdfCIDFont(fontObject),
                PdfTokens.Type1FontKey => new PdfSimpleFont(fontObject),
                PdfTokens.TrueTypeFontKey => new PdfSimpleFont(fontObject),
                PdfTokens.Type3FontKey => new PdfType3Font(fontObject),
                PdfTokens.MMType1FontKey => new PdfSimpleFont(fontObject),
                _ => new PdfSimpleFont(fontObject) // Fallback for unknown subtypes under /Font
            };
        }
    }
}