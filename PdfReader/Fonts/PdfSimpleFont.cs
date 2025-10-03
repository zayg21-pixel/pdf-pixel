using PdfReader.Models;
using System;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Simple fonts: Type1, TrueType, MMType1 (excluding Type3)
    /// Self-contained fonts with direct character-to-glyph mapping
    /// Limited to 256 characters (single-byte encoding)
    /// Uses thread-safe Lazy&lt;T&gt; pattern for heavy operations
    /// </summary>
    public class PdfSimpleFont : PdfSingleByteFont
    {
        // Thread-safe lazy loading using Lazy<T>
        private readonly Lazy<PdfFontDescriptor> _fontDescriptor;

        /// <summary>
        /// Constructor for simple fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF object containing the font definition</param>
        public PdfSimpleFont(PdfObject fontObject) : base(fontObject)
        {
            if (Type == PdfFontType.Type3)
                throw new ArgumentException("Type3 fonts should use PdfType3Font class", nameof(fontObject));

            // Initialize thread-safe lazy loaders
            _fontDescriptor = new Lazy<PdfFontDescriptor>(LoadFontDescriptor, isThreadSafe: true);
        }

        /// <summary>
        /// Font descriptor containing metrics and embedding information
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public override PdfFontDescriptor FontDescriptor => _fontDescriptor.Value;

        /// <summary>
        /// Check if font has embedded data (uses lazy-loaded FontDescriptor)
        /// </summary>
        public override bool IsEmbedded => FontDescriptor?.HasEmbeddedFont == true;

        /// <summary>
        /// Load font descriptor (heavy operation - lazy loaded using GetPageObject)
        /// </summary>
        private PdfFontDescriptor LoadFontDescriptor()
        {
            try
            {
                var descriptorDict = Dictionary.GetDictionary(PdfTokens.FontDescriptorKey);
                return descriptorDict != null ? PdfFontDescriptor.FromDictionary(descriptorDict) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}