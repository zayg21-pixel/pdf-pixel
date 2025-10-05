using CommunityToolkit.HighPerformance;
using PdfReader.Models;
using PdfReader.Streams;
using System;

namespace PdfReader.Fonts
{
    /// <summary>
    /// CID fonts: CIDFontType0, CIDFontType2
    /// Contains actual font data and glyph mappings for multi-byte character support
    /// Used as descendant fonts in Type0 composite fonts
    /// Uses thread-safe Lazy&lt;T&gt; pattern for heavy operations
    /// </summary>
    public class PdfCIDFont : PdfFontBase
    {
        // Thread-safe lazy loading using Lazy<T>
        private readonly Lazy<PdfFontDescriptor> _fontDescriptor;
        private readonly Lazy<PdfCIDSystemInfo> _cidSystemInfo;
        private readonly Lazy<PdfCIDToGIDMap> _cidToGIDMap;

        /// <summary>
        /// Constructor for CID fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF object containing the font definition</param>
        public PdfCIDFont(PdfObject fontObject) : base(fontObject)
        {
            // Lightweight dictionary operations in constructor
            Widths = new PdfFontWidths
            {
                DefaultWidth = fontObject.Dictionary.GetFloatOrDefault(PdfTokens.DWKey)
            };
            
            // Set PDF default if not specified
            if (Widths.DefaultWidth == 0)
                Widths.DefaultWidth = 1000;

            // Initialize thread-safe lazy loaders (no more reference storage)
            _fontDescriptor = new Lazy<PdfFontDescriptor>(LoadFontDescriptor, isThreadSafe: true);
            _cidSystemInfo = new Lazy<PdfCIDSystemInfo>(LoadCIDSystemInfo, isThreadSafe: true);
            _cidToGIDMap = new Lazy<PdfCIDToGIDMap>(LoadCIDToGIDMap, isThreadSafe: true);
        }

        /// <summary>
        /// Font descriptor containing metrics and embedding information
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public override PdfFontDescriptor FontDescriptor => _fontDescriptor.Value;
        
        /// <summary>
        /// CID system information (Registry, Ordering, Supplement)
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public PdfCIDSystemInfo CIDSystemInfo => _cidSystemInfo.Value;
        
        /// <summary>
        /// Character width information for CID-based characters
        /// Initialized during construction
        /// </summary>
        public PdfFontWidths Widths { get; }
        
        /// <summary>
        /// Loaded CID-to-GID mapping
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public PdfCIDToGIDMap CIDToGIDMap => _cidToGIDMap.Value;

        /// <summary>
        /// Check if font has embedded data (uses lazy-loaded FontDescriptor)
        /// </summary>
        public override bool IsEmbedded => FontDescriptor?.HasEmbeddedFont == true;

        /// <summary>
        /// Get character width for a given character code
        /// </summary>
        public override float GetGlyphWidth(int charCode)
        {
            return Widths?.GetWidth(charCode) ?? Widths?.DefaultWidth ?? 1000f;
        }
        
        /// <summary>
        /// Convert Character ID (CID) to Glyph ID (GID) for font rendering
        /// Uses lazy-loaded CIDToGIDMap or defaults to identity mapping
        /// </summary>
        public uint GetGlyphId(uint cid)
        {
            return CIDToGIDMap?.GetGID(cid) ?? cid;
        }
        
        /// <summary>
        /// Check if this CID font has a valid character collection definition
        /// </summary>
        public bool HasValidCIDSystemInfo => CIDSystemInfo != null;
        
        /// <summary>
        /// Check if CID-to-GID mapping is available
        /// </summary>
        public bool HasCIDToGIDMapping => CIDToGIDMap != null;

        /// <summary>
        /// Load font descriptor (heavy operation - lazy loaded)
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
        /// Load CID system info (heavy operation - lazy loaded)
        /// </summary>
        private PdfCIDSystemInfo LoadCIDSystemInfo()
        {
            try
            {
                var cidSystemInfoDict = Dictionary.GetDictionary(PdfTokens.CIDSystemInfoKey);
                return cidSystemInfoDict != null ? PdfCIDSystemInfo.FromDictionary(cidSystemInfoDict) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Load CIDToGIDMap (heavy operation - lazy loaded using GetPageObject)
        /// </summary>
        private PdfCIDToGIDMap LoadCIDToGIDMap()
        {
            // Use GetPageObject instead of stored reference
            var cidToGidObj = Dictionary.GetPageObject(PdfTokens.CIDToGIDMapKey);
            if (cidToGidObj != null)
            {
                try
                {
                    // Load as stream data
                    var cidToGidData = PdfStreamDecoder.DecodeContentStream(cidToGidObj);
                    var map = PdfCIDToGIDMap.FromStreamData(cidToGidData);
                    return map;
                }
                catch (Exception)
                {
                    // Fall through to other methods
                }
            }
            
            // Check if CIDToGIDMap is specified as "Identity" in the font dictionary
            var cidToGidName = Dictionary.GetName(PdfTokens.CIDToGIDMapKey);
            if (cidToGidName == "Identity")
            {
                return PdfCIDToGIDMap.CreateIdentityMapping();
            }

            // According to PDF spec, if no CIDToGIDMap is specified for CIDFontType2, it defaults to Identity
            if (Type == PdfFontType.CIDFontType2)
            {
                return PdfCIDToGIDMap.CreateIdentityMapping();
            }

            return null;
        }
    }
}