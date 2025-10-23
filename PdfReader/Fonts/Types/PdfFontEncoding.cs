namespace PdfReader.Fonts.Types
{
    public enum PdfFontEncoding
    {
        StandardEncoding,
        MacRomanEncoding,
        WinAnsiEncoding,
        MacExpertEncoding,
        IdentityH,        // Identity-H (horizontal CID encoding)
        IdentityV,        // Identity-V (vertical CID encoding)
        // Predefined Unicode CMaps for CJK (Type0/CID fonts)
        UniJIS_UTF16_H,
        UniJIS_UTF16_V,
        UniGB_UTF16_H,
        UniGB_UTF16_V,
        UniCNS_UTF16_H,
        UniCNS_UTF16_V,
        UniKS_UTF16_H,
        UniKS_UTF16_V,
        Custom,
        Unknown
    }
}