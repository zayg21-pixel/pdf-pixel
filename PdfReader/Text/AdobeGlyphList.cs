using PdfReader.Models;
using System.Collections.Generic;

namespace PdfReader.Text
{
    internal static class AdobeGlyphList
    {
        static AdobeGlyphList()
        {
            var aglData = PdfTextResourceConverter.ReadFromResource("Agl.bin");
            CharacterMap = PdfTextResourceConverter.ReadFromCharacterMapBlob(aglData);
        }

        public static Dictionary<PdfString, string> CharacterMap { get; }
    }
}
