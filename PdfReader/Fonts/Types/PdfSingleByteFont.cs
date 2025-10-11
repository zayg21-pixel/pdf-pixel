using PdfReader.Models;
using System;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// Intermediate base class for single-byte fonts (Simple fonts and Type3 fonts)
    /// Provides common functionality for fonts limited to 256 characters with single-byte encoding
    /// Uses thread-safe Lazy&lt;T&gt; pattern for heavy operations
    /// </summary>
    public abstract class PdfSingleByteFont : PdfFontBase
    {
        /// <summary>
        /// Constructor for single-byte fonts - handles common initialization
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        public PdfSingleByteFont(PdfDictionary fontDictionary) : base(fontDictionary)
        {            
            Widths = new PdfFontWidths
            {
                FirstChar = Dictionary.GetIntegerOrDefault(PdfTokens.FirstCharKey),
                LastChar = Dictionary.GetIntegerOrDefault(PdfTokens.LastCharKey),
                Widths = Dictionary.GetArray(PdfTokens.WidthsKey).GetFloatArray() ?? []
            };
        }
        
        /// <summary>
        /// Character width information
        /// Initialized during construction
        /// </summary>
        public PdfFontWidths Widths { get; }

        /// <summary>
        /// Get character width from font metrics
        /// </summary>
        public override float GetGlyphWidth(int charCode)
        {
            return Widths?.GetWidth(charCode) ?? 1000f;
        }

        /// <summary>
        /// Parse font encoding from dictionary (lightweight operation)
        /// </summary>
        protected static PdfFontEncoding ParseEncoding(PdfDictionary dict)
        {
            var encoding = dict.GetName(PdfTokens.EncodingKey);
            
            return encoding switch
            {
                PdfTokens.StandardEncodingKey => PdfFontEncoding.StandardEncoding,
                PdfTokens.MacRomanEncodingKey => PdfFontEncoding.MacRomanEncoding,
                PdfTokens.WinAnsiEncodingKey => PdfFontEncoding.WinAnsiEncoding,
                PdfTokens.MacExpertEncodingKey => PdfFontEncoding.MacExpertEncoding,
                PdfTokens.IdentityHEncodingKey => PdfFontEncoding.IdentityH,
                PdfTokens.IdentityVEncodingKey => PdfFontEncoding.IdentityV,
                null => PdfFontEncoding.Unknown,
                _ => PdfFontEncoding.Custom
            };
        }
    }
}