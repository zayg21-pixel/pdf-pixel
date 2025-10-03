using PdfReader.Models;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts
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
        private readonly Lazy<PdfCodeToCidCMap> _codeToCidCMap;

        /// <summary>
        /// Constructor for composite fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF object containing the font definition</param>
        public PdfCompositeFont(PdfObject fontObject) : base(fontObject)
        {
            // Initialize thread-safe lazy loaders
            _descendantFonts = new Lazy<List<PdfCIDFont>>(LoadDescendantFonts, isThreadSafe: true);
            _codeToCidCMap = new Lazy<PdfCodeToCidCMap>(LoadCodeToCidCMap, isThreadSafe: true);
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
        public PdfCodeToCidCMap CodeToCidCMap => _codeToCidCMap.Value;

        /// <summary>
        /// Check if font has embedded data (delegated to primary descendant)
        /// </summary>
        public override bool IsEmbedded => PrimaryDescendant?.IsEmbedded == true;

        /// <summary>
        /// Get character width (delegated to appropriate descendant)
        /// </summary>
        public override float GetGlyphWidth(int charCode)
        {
            var descendant = PrimaryDescendant;
            return descendant?.GetGlyphWidth(charCode) ?? 1000f;
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
            
            try
            {
                // Use GetPageObjects to get all descendant font objects
                var descendantObjects = Dictionary.GetPageObjects(PdfTokens.DescendantFontsKey);
                if (descendantObjects == null || descendantObjects.Count == 0)
                {
                    return descendants;
                }

                foreach (var descendantObj in descendantObjects)
                {
                    if (descendantObj == null)
                    {
                        continue;
                    }

                    var descendantRef = descendantObj.Reference;

                    // Check if already loaded in document cache
                    if (Document.Fonts.TryGetValue(descendantRef, out var cachedDescendant) && 
                        cachedDescendant is PdfCIDFont cachedCIDFont)
                    {
                        descendants.Add(cachedCIDFont);
                        continue;
                    }

                    // Load descendant font using factory
                    var descendantFont = PdfFontFactory.CreateFont(descendantObj);
                    if (descendantFont is PdfCIDFont cidFont)
                    {
                        descendants.Add(cidFont);
                        // Cache the descendant font in document
                        Document.Fonts[descendantRef] = cidFont;
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list on error
            }

            return descendants;
        }

        /// <summary>
        /// Load an embedded /Encoding CMap stream (if present) into a code->CID map.
        /// Returns null if /Encoding is a name or if parsing fails.
        /// </summary>
        private PdfCodeToCidCMap LoadCodeToCidCMap()
        {
            try
            {
                var encodingObj = Dictionary.GetPageObject(PdfTokens.EncodingKey);
                if (encodingObj == null)
                {
                    return null;
                }

                var data = PdfStreamDecoder.DecodeContentStream(encodingObj);
                if (data.IsEmpty || data.Length == 0)
                {
                    return null;
                }

                var ctx = new PdfParseContext(data);
                return PdfCodeToCidCMapParser.ParseCMapFromContext(ref ctx, Document);
            }
            catch (Exception)
            {
                // Ignore errors; fall back to Identity or no mapping
                return null;
            }
        }
    }
}