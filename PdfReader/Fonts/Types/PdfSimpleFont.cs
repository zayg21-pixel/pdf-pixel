using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.Types;
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
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        public PdfSimpleFont(PdfDictionary fontDictionary) : base(fontDictionary)
        {
            if (Type == PdfFontType.Type3)
                throw new ArgumentException("Type3 fonts should use PdfType3Font class");

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
        /// Indicates whether this font requires shaping for correct glyph mapping.
        /// Returns false if CFF info is present and encoding/differences are resolved; otherwise true.
        /// </summary>
        public override bool ShouldShape
        {
            get
            {
                var cffInfo = FontDescriptor?.GetCffInfo();
                if (cffInfo != null)
                {
                    // CFF info present, encoding and differences resolved
                    return false;
                }
                // Fallback: shaping required
                return true;
            }
        }

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

            var cff = FontDescriptor?.GetCffInfo();
            if (cff == null)
            {
                return 0;
            }

            uint cid = code;
            string name = null;
            bool hasDifference = Differences != null && Differences.TryGetValue((int)cid, out name) && !string.IsNullOrEmpty(name);

            if (hasDifference && cff.NameToGid.TryGetValue(name, out ushort namedGid))
            {
                return namedGid;
            }

            if (!hasDifference &&
                SingleByteEncodingConverter.TryGetNameByCid(cid, Encoding, out string nameByCid) &&
                cff.NameToGid.TryGetValue(nameByCid, out var standardNamedGid))
            {
                return standardNamedGid;
            }

            return 0;
        }
    }
}