using PdfReader.Text;

namespace PdfReader.Fonts
{
    [PdfEnum]
    public enum PdfFontSubType
    {
        [PdfEnumDefaultValue]
        Unknown,

        [PdfEnumValue("Type0")]
        Type0,

        [PdfEnumValue("Type1")]
        Type1,

        [PdfEnumValue("MMType1")]
        MMType1,

        [PdfEnumValue("Type3")]
        Type3,

        [PdfEnumValue("TrueType")]
        TrueType,

        [PdfEnumValue("CIDFontType0")]
        CidFontType0,

        [PdfEnumValue("CIDFontType2")]
        CidFontType2
    }
}