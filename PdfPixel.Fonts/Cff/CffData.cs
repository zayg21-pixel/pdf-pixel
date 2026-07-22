using PdfPixel.Fonts.Resources;
using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.Cff;

internal static class CffData
{
    static CffData()
    {
        byte[] standardStringsData = FontResourceLoader.GetResource("StandardStrings.bin");
        StandardStrings = FontResourceConverter.FromPdfStringBlob(standardStringsData);

        byte[] isoAdobeStringsData = FontResourceLoader.GetResource("IsoAdobeStrings.bin");
        IsoAdobeStrings = FontResourceConverter.FromPdfStringBlob(isoAdobeStringsData);

        byte[] expertStringsData = FontResourceLoader.GetResource("ExpertStrings.bin");
        ExpertStrings = FontResourceConverter.FromPdfStringBlob(expertStringsData);

        byte[] expertSubsetStringsData = FontResourceLoader.GetResource("ExpertSubsetStrings.bin");
        ExpertSubsetStrings = FontResourceConverter.FromPdfStringBlob(expertSubsetStringsData);
    }

    internal static readonly PdfFontString[] StandardStrings;
    internal static readonly PdfFontString[] IsoAdobeStrings;
    internal static readonly PdfFontString[] ExpertStrings;
    internal static readonly PdfFontString[] ExpertSubsetStrings;
}
