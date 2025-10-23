using System.Collections.Generic;
using PdfReader.Fonts.Types;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Holds encoding information parsed from a PDF font dictionary.
    /// </summary>
    public struct PdfFontEncodingInfo
    {
        public PdfFontEncodingInfo(PdfFontEncoding encoding, string customEncoding, Dictionary<int, string> differences)
        {
            Encoding = encoding;
            CustomEncoding = customEncoding;
            Differences = differences;
        }

        /// <summary>
        /// The resolved base encoding enum (or Identity encodings for CID), or Unknown if not present.
        /// </summary>
        public PdfFontEncoding Encoding { get; }
        /// <summary>
        /// Custom encoding name (when Encoding == Custom). For name-based encodings not recognized.
        /// </summary>
        public string CustomEncoding { get; }
        /// <summary>
        /// Differences array parsed from /Encoding dictionary as a code -> glyph name map.
        /// Empty for name-based encodings or when not present.
        /// </summary>
        public Dictionary<int, string> Differences { get; }
    }
}
