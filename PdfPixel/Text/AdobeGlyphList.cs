using PdfPixel.Models;
using PdfPixel.Resources;
using System.Collections.Generic;

namespace PdfPixel.Text
{
    /// <summary>
    /// Represents Adobe Glyph list as map of characters.
    /// </summary>
    internal static class AdobeGlyphList
    {
        static AdobeGlyphList()
        {
            byte[] aglData = PdfResourceLoader.GetResource("External.glyphlist.bin");
            CharacterMap = new Dictionary<PdfString, string>();
            PdfTextResourceConverter.ReadFromCharacterMapBlob(aglData, CharacterMap);

            // AGL Overrides contains overrides for AGL Symbols from private user area
            byte[] aglOverridesData = PdfResourceLoader.GetResource("AglOverrides.bin");
            PdfTextResourceConverter.ReadFromCharacterMapBlob(aglOverridesData, CharacterMap);

            //AGL Zapf Dingbats Unicode symbols
            byte[] aglZapfDingbatsData = PdfResourceLoader.GetResource("External.zapfdingbats.bin");
            PdfTextResourceConverter.ReadFromCharacterMapBlob(aglZapfDingbatsData, CharacterMap);
        }

        /// <summary>
        /// Merged AGL with overrides for PUA and Zapf Dingbats symbols.
        /// </summary>
        public static Dictionary<PdfString, string> CharacterMap { get; }
    }
}
