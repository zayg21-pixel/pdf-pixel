using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Text;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// Type0 (Composite) fonts: Multi-byte character support
    /// Acts as a wrapper that delegates to descendant CID fonts for actual rendering
    /// Handles character encoding and script/language coordination
    /// Uses thread-safe Lazy&lt;T&gt; pattern for heavy operations
    /// </summary>
    public class PdfCompositeFont : PdfFontBase
    {
        // Thread-safe lazy loading using Lazy<T>
        private readonly Lazy<List<PdfCIDFont>> _descendantFonts;
        private readonly Lazy<PdfCMap> _codeToCidCMap;

        /// <summary>
        /// Constructor for composite fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        public PdfCompositeFont(PdfDictionary fontDictionary) : base(fontDictionary)
        {
            // Initialize thread-safe lazy loaders
            _descendantFonts = new Lazy<List<PdfCIDFont>>(LoadDescendantFonts, isThreadSafe: true);
            _codeToCidCMap = new Lazy<PdfCMap>(LoadCodeToCidCMap, isThreadSafe: true);
        }

        /// <summary>
        /// Get font descriptor (delegated to primary descendant)
        /// Type0 fonts don't have their own FontDescriptor - it's in the descendant
        /// </summary>
        public override PdfFontDescriptor FontDescriptor => PrimaryDescendant?.FontDescriptor;
        
        /// <summary>
        /// Descendant CID fonts that contain the actual font data
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// Usually contains one font, but can have multiple for multi-script support
        /// </summary>
        public List<PdfCIDFont> DescendantFonts => _descendantFonts.Value;
        
        /// <summary>
        /// Primary descendant font (first in array, handles most characters)
        /// This is where most properties are inherited from
        /// </summary>
        public PdfCIDFont PrimaryDescendant => DescendantFonts.Count > 0 ? DescendantFonts[0] : null;

        /// <summary>
        /// Optional code->CID CMap derived from the parent /Encoding entry when it is a CMap stream.
        /// May be null if /Encoding is a predefined name without an embedded stream (e.g., Identity-H).
        /// </summary>
        public PdfCMap CodeToCidCMap => _codeToCidCMap.Value;

        /// <summary>
        /// Check if font has embedded data (delegated to primary descendant)
        /// </summary>
        public override bool IsEmbedded => PrimaryDescendant?.IsEmbedded == true;

        /// <summary>
        /// Get character width (delegated to appropriate descendant CID font by CID).
        /// </summary>
        public override float GetWidth(PdfCharacterCode code)
        {
            var descendant = PrimaryDescendant;
            if (descendant == null)
            {
                return 0f;
            }

            uint cid;

            if (!TryMapCodeToCid(code, out cid))
            {
                return 0f;
            }

            return descendant.GetWidthByCid(cid);
        }

        /// <summary>
        /// Try to map a length-aware content code (PdfCid) to a numeric CID using the parent encoding.
        /// For Identity-H/V, the mapping is an identity of the big-endian integer value.
        /// For embedded CMap streams, uses the parsed CodeToCidCMap.
        /// </summary>
        public bool TryMapCodeToCid(PdfCharacterCode code, out uint cid)
        {
            // Identity encodings: code bytes represent CID directly
            if (Encoding == PdfFontEncoding.IdentityH || Encoding == PdfFontEncoding.IdentityV)
            {
                cid = (uint)code;
                return true;
            }

            var map = CodeToCidCMap;
            if (map != null && map.TryGetCid(code, out int mapped))
            {
                cid = (uint)mapped;
                return true;
            }

            cid = 0;
            return false;
        }

        /// <summary>
        /// Load descendant fonts (heavy operation - lazy loaded using GetPageObjects)
        /// </summary>
        private List<PdfCIDFont> LoadDescendantFonts()
        {
            var descendants = new List<PdfCIDFont>();

            // Use GetPageObjects to get all descendant font objects
            var descendantObjects = Dictionary.GetObjects(PdfTokens.DescendantFontsKey);
            if (descendantObjects == null || descendantObjects.Count == 0)
            {
                return descendants;
            }

            foreach (var descendantObj in descendantObjects)
            {
                var descendant = PdfFontFactory.CreateFont(descendantObj.Dictionary);

                if (descendant is PdfCIDFont cidFont)
                {
                    descendants.Add(cidFont);
                }

            }

            return descendants;
        }

        /// <summary>
        /// Load an embedded /Encoding CMap stream (if present) into a code->CID map.
        /// Returns null if /Encoding is a name or if parsing fails.
        /// </summary>
        private PdfCMap LoadCodeToCidCMap()
        {
            var encodingObj = Dictionary.GetObject(PdfTokens.EncodingKey);
            if (encodingObj == null)
            {
                return null;
            }

            var data = encodingObj.DecodeAsMemory();
            if (data.IsEmpty || data.Length == 0)
            {
                return null;
            }

            var ctx = new PdfParseContext(data);
            return PdfCMapParser.ParseCMapFromContext(ref ctx, Document);
        }

        /// <summary>
        /// Extracts character codes from raw bytes for composite fonts.
        /// Uses codespace ranges if ToUnicodeCMap is available and valid; otherwise uses code length.
        /// </summary>
        /// <param name="bytes">Raw bytes to extract character codes from.</param>
        /// <returns>Array of extracted PdfCharacterCode items.</returns>
        public override PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return Array.Empty<PdfCharacterCode>();
            }

            if (CodeToCidCMap != null && CodeToCidCMap.HasCodeSpaceRanges)
            {
                var cmap = CodeToCidCMap;
                var characterCodes = new List<PdfCharacterCode>();
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int length = cmap.GetMaxMatchingLength(bytes.Slice(offset).Span);
                    if (length == 0)
                    {
                        length = 1;
                    }
                    characterCodes.Add(new PdfCharacterCode(bytes.Slice(offset, length)));
                    offset += length;
                }
                return characterCodes.ToArray();
            }

            int codeLength = GetCharacterCodeLength();
            if (codeLength == 2 && bytes.Length % 2 != 0)
            {
                // Odd byte count with 2-byte expectation, fallback to 1-byte segmentation
                codeLength = 1;
            }

            int count = bytes.Length / codeLength;
            var result = new PdfCharacterCode[count];
            for (int index = 0; index < count; index++)
            {
                int offset = index * codeLength;
                result[index] = new PdfCharacterCode(bytes.Slice(offset, codeLength));
            }
            return result;
        }

        /// <summary>
        /// Determines the character code length for this composite font.
        /// </summary>
        /// <returns>The code length in bytes (1 or 2).</returns>
        private int GetCharacterCodeLength()
        {
            switch (Encoding)
            {
                case PdfFontEncoding.IdentityH:
                case PdfFontEncoding.IdentityV:
                case PdfFontEncoding.UniJIS_UTF16_H:
                case PdfFontEncoding.UniJIS_UTF16_V:
                case PdfFontEncoding.UniGB_UTF16_H:
                case PdfFontEncoding.UniGB_UTF16_V:
                case PdfFontEncoding.UniCNS_UTF16_H:
                case PdfFontEncoding.UniCNS_UTF16_V:
                case PdfFontEncoding.UniKS_UTF16_H:
                case PdfFontEncoding.UniKS_UTF16_V:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Gets the glyph ID (GID) for the specified character code in a composite font.
        /// Returns 0 if no valid GID is found.
        /// Follows PDF spec: character code is mapped to CID using encoding/CMap, then CID is mapped to GID by descendant font.
        /// </summary>
        /// <param name="code">The character code to map to a glyph ID.</param>
        /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
        public override ushort GetGid(PdfCharacterCode code)
        {
            if (code == null)
            {
                return 0;
            }

            var descendant = PrimaryDescendant;
            if (descendant == null)
            {
                return 0;
            }

            uint cid;
            if (!TryMapCodeToCid(code, out cid))
            {
                return 0;
            }

            // Call GetGlyphId of CID font directly
            return descendant.GetGidByCid(cid);
        }
    }
}