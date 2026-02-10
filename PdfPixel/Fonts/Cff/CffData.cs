using PdfPixel.Models;
using PdfPixel.Resources;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Cff;

internal static class CffData
{
    static CffData()
    {
        var standardStringsData = PdfResourceLoader.GetResource("StandardStrings.bin");
        StandardStrings = PdfTextResourceConverter.FromPdfStringBlob(standardStringsData);

        var isoAdobeStringsData = PdfResourceLoader.GetResource("IsoAdobeStrings.bin");
        IsoAdobeStrings = PdfTextResourceConverter.FromPdfStringBlob(isoAdobeStringsData);

        var expertStringsData = PdfResourceLoader.GetResource("ExpertStrings.bin");
        ExpertStrings = PdfTextResourceConverter.FromPdfStringBlob(expertStringsData);

        var expertSubsetStringsData = PdfResourceLoader.GetResource("ExpertSubsetStrings.bin");
        ExpertSubsetStrings = PdfTextResourceConverter.FromPdfStringBlob(expertSubsetStringsData);
    }

    internal static readonly PdfString[] StandardStrings;
    internal static readonly PdfString[] IsoAdobeStrings;
    internal static readonly PdfString[] ExpertStrings;
    internal static readonly PdfString[] ExpertSubsetStrings;
}
