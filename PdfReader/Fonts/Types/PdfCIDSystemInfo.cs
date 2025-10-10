using PdfReader.Models;

namespace PdfReader.Fonts.Types
{
    // CID font system info
    public class PdfCIDSystemInfo
    {
        public string Registry { get; set; }
        public string Ordering { get; set; }
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