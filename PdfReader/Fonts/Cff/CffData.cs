using PdfReader.Models;
using PdfReader.Text;
using System.IO;

namespace PdfReader.Fonts.Cff
{
    internal static class CffData
    {
        static CffData()
        {
            var standardStringsData = PdfTextResourceConverter.ReadFromResource("StandardStrings.bin");
            StandardStrings = PdfTextResourceConverter.FromPdfStringBlob(standardStringsData);

            var isoAdobeStringsData = PdfTextResourceConverter.ReadFromResource("IsoAdobeStrings.bin");
            IsoAdobeStrings = PdfTextResourceConverter.FromPdfStringBlob(isoAdobeStringsData);

            var expertStringsData = PdfTextResourceConverter.ReadFromResource("ExpertStrings.bin");
            ExpertStrings = PdfTextResourceConverter.FromPdfStringBlob(expertStringsData);

            var expertSubsetStringsData = PdfTextResourceConverter.ReadFromResource("ExpertSubsetStrings.bin");
            ExpertSubsetStrings = PdfTextResourceConverter.FromPdfStringBlob(expertSubsetStringsData);
        }

        internal static readonly PdfString[] StandardStrings;
        internal static readonly PdfString[] IsoAdobeStrings;
        internal static readonly PdfString[] ExpertStrings;
        internal static readonly PdfString[] ExpertSubsetStrings;
    }
}
