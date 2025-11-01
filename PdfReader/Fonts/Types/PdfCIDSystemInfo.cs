using PdfReader.Models;

namespace PdfReader.Fonts.Types
{
    // CID font system info
    public class PdfCIDSystemInfo
    {
        public PdfString Registry { get; set; }
        public PdfString Ordering { get; set; }
        public int Supplement { get; set; }

        public static PdfCIDSystemInfo FromDictionary(PdfDictionary dict)
        {
            return new PdfCIDSystemInfo
            {
                Registry = dict.GetString(PdfTokens.RegistryKey),
                Ordering = dict.GetString(PdfTokens.OrderingKey),
                Supplement = dict.GetIntegerOrDefault(PdfTokens.SupplementKey)
            };
        }
    }
}