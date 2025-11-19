using PdfReader.Text;

namespace PdfReader.Fonts.Types;

/// <summary>
/// Encoding for CID fonts (Type0 fonts)
/// </summary>
[PdfEnum]
public enum PdfCidFontEncoding
{
    [PdfEnumDefaultValue]
    Unknown,

    [PdfEnumValue("Identity-H")]
    IdentityH,
    [PdfEnumValue("Identity-V")]
    IdentityV,
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