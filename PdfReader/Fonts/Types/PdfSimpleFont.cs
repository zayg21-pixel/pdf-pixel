using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using SkiaSharp;
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
        private readonly ILogger<PdfSimpleFont> _logger;
        private readonly IByteCodeToGidMapper _mapper;

        /// <summary>
        /// Constructor for simple fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        public PdfSimpleFont(PdfDictionary fontDictionary) : base(fontDictionary)
        {
            _logger = fontDictionary.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();

            // Initialize thread-safe lazy loaders
            FontDescriptor = LoadFontDescriptor();
            _mapper = Document.FontCache.GetByteCodeToGidMapper(this);
        }

        /// <summary>
        /// Font descriptor containing metrics and embedding information
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public override PdfFontDescriptor FontDescriptor { get; }

        public override PdfFontEncoding Encoding => GetResolvedEncoding(base.Encoding);

        /// <summary>
        /// Check if font has embedded data (uses lazy-loaded FontDescriptor)
        /// </summary>
        public override bool IsEmbedded => FontDescriptor?.HasEmbeddedFont == true;


        public SKPath GetPath(PdfCharacterCode code)
        {
            return null;
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

        private PdfFontEncoding GetResolvedEncoding(PdfFontEncoding baseEncoding)
        {
            if (baseEncoding == PdfFontEncoding.Unknown)
            {
                // TODO: treat CFF correctly

                switch (Type)
                {
                    case PdfFontSubType.TrueType:
                        return PdfFontEncoding.WinAnsiEncoding;
                    default:
                        return PdfFontEncoding.StandardEncoding;
                }
            }

            return baseEncoding;
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

            if (_mapper == null)
            {
                return 0;
            }

            return _mapper.GetGid((byte)code);
        }
    }
}