using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using System;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// CID fonts: CIDFontType0, CIDFontType2
    /// Contains actual font data and glyph mappings for multi-byte character support
    /// Used as descendant fonts in Type0 composite fonts
    /// Uses thread-safe Lazy&lt;T&gt; pattern for heavy operations
    /// </summary>
    public class PdfCIDFont : PdfFontBase
    {
        private readonly Lazy<PdfCIDSystemInfo> _cidSystemInfo;
        private readonly Lazy<PdfCIDToGIDMap> _cidToGIDMap;

        /// <summary>
        /// Constructor for CID fonts - lightweight operations only
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        public PdfCIDFont(PdfDictionary fontDictionary) : base(fontDictionary)
        {
            Widths = CidFontWidths.Parse(fontDictionary);

            // Initialize thread-safe lazy loaders (no more reference storage)
            FontDescriptor = LoadFontDescriptor();
            _cidSystemInfo = new Lazy<PdfCIDSystemInfo>(LoadCIDSystemInfo, isThreadSafe: true);
            _cidToGIDMap = new Lazy<PdfCIDToGIDMap>(LoadCIDToGIDMap, isThreadSafe: true);
        }

        /// <summary>
        /// Font descriptor containing metrics and embedding information
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public override PdfFontDescriptor FontDescriptor { get; }
        
        /// <summary>
        /// CID system information (Registry, Ordering, Supplement)
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public PdfCIDSystemInfo CIDSystemInfo => _cidSystemInfo.Value;

        /// <summary>
        /// Character width information for CID-based characters
        /// Initialized during construction
        /// </summary>
        public CidFontWidths Widths { get; }

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
        /// Gets the width for a given CID in this CID font.
        /// Returns explicit width if defined, otherwise DefaultWidth, otherwise 0f.
        /// </summary>
        /// <param name="cid">The CID to get the width for.</param>
        /// <returns>The width for the CID.</returns>
        public float GetWidthByCid(uint cid)
        {
            var width = Widths.GetWidth(cid);
            if (width.HasValue)
            {
                return width.Value;
            }
            if (Widths.DefaultWidth.HasValue)
            {
                return Widths.DefaultWidth.Value;
            }

            return 0f;
        }

        /// <summary>
        /// Get character width for a given character code
        /// </summary>
        public override float GetWidth(PdfCharacterCode code)
        {
            return GetWidthByCid((uint)code);
        }
        
        /// <summary>
        /// Convert Character ID (CID) to Glyph ID (GID) for font rendering.
        /// Uses lazy-loaded CIDToGIDMap or returns 0 if no mapping exists.
        /// </summary>
        public ushort GetGidByCid(uint cid)
        {
            // If font is not embedded, can't map to GID
            if (!IsEmbedded)
            {
                return 0;
            }

            if (CIDToGIDMap == null)
            {
                return (ushort)cid;
            }

            if (CIDToGIDMap.HasMapping(cid))
            {
                return CIDToGIDMap.GetGID(cid);
            }
            return 0;
        }

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
            // Check if CIDToGIDMap is specified as "Identity" in the font dictionary
            var cidToGidName = Dictionary.GetName(PdfTokens.CIDToGIDMapKey);
            if (cidToGidName == PdfTokens.IdentityKey)
            {
                return PdfCIDToGIDMap.CreateIdentityMapping();
            }

            // Use GetPageObject instead of stored reference
            var cidToGidObj = Dictionary.GetPageObject(PdfTokens.CIDToGIDMapKey);
            if (cidToGidObj != null)
            {
                // Load as stream data
                var cidToGidData = cidToGidObj.DecodeAsMemory();
                return PdfCIDToGIDMap.FromStreamData(cidToGidData);
            }

            var cffFont = Document.FontCache.GetCffInfo(this);

            if (cffFont != null)
            {
                return PdfCIDToGIDMap.FromCffFont(cffFont);
            }

            return null;
        }

        /// <summary>
        /// Extracts character codes from raw bytes for CID fonts.
        /// Always uses fixed-length segmentation (2 bytes per CID).
        /// This method does not use codespace ranges or ToUnicode CMap, as those are only defined at the composite font (Type0) level.
        /// </summary>
        /// <param name="bytes">Raw bytes to extract character codes from.</param>
        /// <returns>Array of extracted PdfCharacterCode items, each representing a 2-byte CID.</returns>
        public override PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return Array.Empty<PdfCharacterCode>();
            }

            const int CodeLength = 2;
            int count = bytes.Length / CodeLength;
            var result = new PdfCharacterCode[count];
            for (int index = 0; index < count; index++)
            {
                int offset = index * CodeLength;
                result[index] = new PdfCharacterCode(bytes.Slice(offset, CodeLength));
            }
            return result;
        }

        /// <summary>
        /// Gets the glyph ID (GID) for the specified character code in a CID font.
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

            uint cid = (uint)code;
            return GetGidByCid(cid);
        }
    }
}