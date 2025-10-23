using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using System;

namespace PdfReader.Fonts.Types
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
        private readonly Lazy<IByteCodeToGidMapper> _mapper;

        /// <summary>
        /// Constructor for simple fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        public PdfSimpleFont(PdfDictionary fontDictionary) : base(fontDictionary)
        {
            if (Type == PdfFontType.Type3)
                throw new ArgumentException("Type3 fonts should use PdfType3Font class");

            // Initialize thread-safe lazy loaders
            _fontDescriptor = new Lazy<PdfFontDescriptor>(LoadFontDescriptor, isThreadSafe: true);
            _mapper = new Lazy<IByteCodeToGidMapper>(() => Document.FontCache.GetByteCodeToGidMapper(this), isThreadSafe: true);
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

        /// <summary>
        /// Gets the glyph ID (GID) for the specified character code in a simple font.
        /// Returns 0 if no valid GID is found.
        /// </summary>
        /// <param name="code">The character code to map to a glyph ID.</param>
        /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
        public override ushort GetGid(PdfCharacterCode code)
        {
            if (code == null)
            {
                return 0;
            }

            string name = SingleByteEncodings.GetNameByCode((byte)(uint)code, Encoding, Differences);

            var mapper = _mapper.Value;

            if (mapper == null)
            {
                return 0;
            }

            return mapper.GetGid((byte)code);
        }
    }
}