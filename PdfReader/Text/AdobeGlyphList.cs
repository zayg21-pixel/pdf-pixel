using PdfReader.Models;
using PdfReader.Resources;
using System.Collections.Generic;

namespace PdfReader.Text
{
    internal static class AdobeGlyphList
    {
        static AdobeGlyphList()
        {
            var aglData = PdfResourceLoader.GetResource("Agl.bin");
            CharacterMap = PdfTextResourceConverter.ReadFromCharacterMapBlob(aglData);
        }

        public static Dictionary<PdfString, string> CharacterMap { get; }
    }
}
