using PdfReader.Text;

namespace PdfReader.Fonts.Types
{
    [PdfEnum]
    public enum PdfFontEncoding
    {
        [PdfEnumDefaultValue]
        Unknown,

        [PdfEnumValue("StandardEncoding")]
        StandardEncoding,
        [PdfEnumValue("MacRomanEncoding")]
        MacRomanEncoding,
        [PdfEnumValue("WinAnsiEncoding")]
        WinAnsiEncoding,
        [PdfEnumValue("MacExpertEncoding")]
        MacExpertEncoding,
        [PdfEnumValue("Identity-H")]
        IdentityH,        // Identity-H (horizontal CID encoding) // TODO: should be removed or have dedicated map
        [PdfEnumValue("Identity-V")]
        IdentityV,        // Identity-V (vertical CID encoding)
        // Predefined Unicode CMaps for CJK (Type0/CID fonts)
        [PdfEnumValue("UniJIS-UTF16-H")]
        UniJIS_UTF16_H,
        [PdfEnumValue("UniJIS-UTF16-V")]
        UniJIS_UTF16_V,
        [PdfEnumValue("UniGB-UTF16-H")]
        UniGB_UTF16_H,
        [PdfEnumValue("UniGB-UTF16-V")]
        UniGB_UTF16_V,
        [PdfEnumValue("UniCNS-UTF16-H")]
        UniCNS_UTF16_H,
        [PdfEnumValue("UniCNS-UTF16-V")]
        UniCNS_UTF16_V,
        [PdfEnumValue("UniKS-UTF16-H")]
        UniKS_UTF16_H,
        [PdfEnumValue("UniKS-UTF16-V")]
        UniKS_UTF16_V
    }
}