using PdfRender.Text;

namespace PdfRender.Streams
{
    [PdfEnum]
    public enum PdfFilterType
    {
        [PdfEnumDefaultValue]
        Unknown,

        [PdfEnumValue("FlateDecode")]
        FlateDecode,

        [PdfEnumValue("LZWDecode")]
        LZWDecode,

        [PdfEnumValue("DCTDecode")]
        DCTDecode,

        [PdfEnumValue("ASCIIHexDecode")]
        ASCIIHexDecode,

        [PdfEnumValue("ASCII85Decode")]
        ASCII85Decode,

        [PdfEnumValue("JPXDecode")]
        JPXDecode,

        [PdfEnumValue("JBIG2Decode")]
        JBIG2Decode,

        [PdfEnumValue("CCITTFaxDecode")]
        CCITTFaxDecode,

        [PdfEnumValue("RunLengthDecode")]
        RunLengthDecode,

        [PdfEnumValue("Crypt")]
        Crypt
    }
}