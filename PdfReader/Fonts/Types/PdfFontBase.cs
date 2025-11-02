using PdfReader.Fonts.Management;
using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PdfReader.Fonts.Types
{
    /// <summary>
    /// Base class for all PDF font types with common properties and interface
    /// Provides the foundation for the proper font hierarchy according to PDF specification
    /// Essential properties are read-only and set through constructor for immutability
    /// Heavy operations are lazy-loaded using thread-safe Lazy&lt;T&gt; pattern
    /// </summary>
    public abstract class PdfFontBase : IDisposable
    {
        private readonly Lazy<PdfCMap> _toUnicodeCMap;
        private readonly ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo> _characterInfoCache = new ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo>();
        private bool disposedValue;

        /// <summary>
        /// Constructor for all PDF fonts with essential immutable properties
        /// Performs only lightweight dictionary operations
        /// </summary>
        /// <param name="fontObject">PDF dictionary containing the font definition</param>
        protected PdfFontBase(PdfDictionary fontDictionary)
        {
            Dictionary = fontDictionary ?? throw new ArgumentNullException(nameof(fontDictionary));

            // Parse encoding and differences from /Encoding (handles name or dictionary cases)
            var encodingInfo = PdfFontEncodingParser.ParseEncoding(fontDictionary);
            Encoding = encodingInfo.Encoding;
            CustomEncoding = encodingInfo.CustomEncoding;
            Differences = encodingInfo.Differences ?? new Dictionary<int, PdfString>();

            // Parse essential properties from the font object (lightweight operations)
            Type = fontDictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();
            BaseFont = fontDictionary.GetString(PdfTokens.BaseFontKey);
            
            // Initialize lazy loaders (thread-safe)
            _toUnicodeCMap = new Lazy<PdfCMap>(LoadToUnicodeCMap, isThreadSafe: true);
        }

        /// <summary>
        /// Font dictionary.
        /// </summary>
        public PdfDictionary Dictionary { get; }

        /// <summary>
        /// PDF font type (Type1, TrueType, Type3, Type0, CIDFontType0, CIDFontType2, etc.)
        /// </summary>
        public PdfFontSubType Type { get; }

        /// <summary>
        /// Character encoding for this font (base encoding or CMap name type for CID fonts)
        /// </summary>
        public virtual PdfFontEncoding Encoding { get; }

        /// <summary>
        /// Custom encoding name. For name-based encodings not recognized.
        /// </summary>
        public PdfString CustomEncoding { get; }

        /// <summary>
        /// Differences array parsed from /Encoding dictionary as a code -> glyph name map.
        /// Empty for name-based encodings or when not present.
        /// </summary>
        public Dictionary<int, PdfString> Differences { get; }

        /// <summary>
        /// Base font name (PostScript name)
        /// </summary>
        public PdfString BaseFont { get; }
        
        /// <summary>
        /// PDF document containing this font (convenience property)
        /// </summary>
        public PdfDocument Document => Dictionary.Document;
        
        /// <summary>
        /// Loaded ToUnicode CMap for character-to-Unicode mapping
        /// Thread-safe lazy-loaded when first accessed - heavy operation
        /// </summary>
        public PdfCMap ToUnicodeCMap => _toUnicodeCMap.Value;

        /// <summary>
        /// Check if this font has embedded font data
        /// </summary>
        public abstract bool IsEmbedded { get; }
        
        /// <summary>
        /// Get the font descriptor (contains metrics and embedding info)
        /// May be direct or inherited from descendant fonts
        /// Implementation may use lazy loading
        /// </summary>
        public abstract PdfFontDescriptor FontDescriptor { get; }
        
        /// <summary>
        /// Get the width of a character/glyph
        /// Implementation varies by font type
        /// </summary>
        public abstract float GetWidth(PdfCharacterCode code);

        /// <summary>
        /// Converts a <see cref="PdfCharacterCode"/> to its corresponding Unicode string representation.
        /// </summary>
        /// <param name="code">The <see cref="PdfCharacterCode"/> to be converted. Cannot be <see langword="null"/>.</param>
        /// <returns>The Unicode string representation of the specified <see cref="PdfCharacterCode"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="code"/> is <see langword="null"/>.</exception>
        public string GetUnicodeString(PdfCharacterCode code)
        {
            if (ToUnicodeCMap != null)
            {
                var unicode = ToUnicodeCMap.GetUnicode(code);
                if (unicode != null)
                {
                    return unicode;
                }
            }

            PdfString name = SingleByteEncodings.GetNameByCodeOrUndefined((byte)(uint)code, Encoding, Differences);

            if (AdobeGlyphList.CharacterMap.TryGetValue(name, out var aglUnicode))
            {
                return aglUnicode;
            }

            return null;
        }

        /// <summary>
        /// Extracts character codes from raw bytes for this font.
        /// Abstract in base; must be overridden in derived font types.
        /// </summary>
        /// <param name="bytes">Raw bytes to extract character codes from.</param>
        /// <returns>Array of extracted PdfCharacterCode items.</returns>
        public abstract PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes);

        /// <summary>
        /// Gets the glyph ID (GID) for the specified character code.
        /// Returns 0 if no valid GID is found.
        /// </summary>
        /// <param name="code">The character code to map to a glyph ID.</param>
        /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
        public abstract ushort GetGid(PdfCharacterCode code);

        /// <summary>
        /// Extracts all resolved information for a single PDF character code.
        /// Caches results for each character code. Calls the protected virtual ExtractCharacterInfoCore for font-specific logic.
        /// </summary>
        /// <param name="characterCode">The character code to extract info for.</param>
        /// <returns>Resolved character info including Unicode, GIDs, and widths.</returns>
        public PdfCharacterInfo ExtractCharacterInfo(PdfCharacterCode characterCode)
        {
            if (characterCode == null)
            {
                throw new ArgumentNullException(nameof(characterCode));
            }

            return _characterInfoCache.GetOrAdd(characterCode, ExtractCharacterInfoCore);
        }

        /// <summary>
        /// Core extraction logic for character info. Override in derived font types.
        /// </summary>
        /// <param name="characterCode">The character code to extract info for.</param>
        /// <returns>Resolved character info including Unicode, GIDs, and widths.</returns>
        protected virtual PdfCharacterInfo ExtractCharacterInfoCore(PdfCharacterCode characterCode)
        {
            ushort gid = GetGid(characterCode);
            float width = GetWidth(characterCode);
            string unicode = GetUnicodeString(characterCode);

            if (gid != 0 && width != 0)
            {
                return new PdfCharacterInfo(characterCode, unicode, gid, width);
            }
            else if (gid != 0 && unicode?.Length > 0)
            {
                using SKFont skFont = GetSkFont();
                float[] widths = skFont.GetGlyphWidths([gid]);
                float measuredWidth = widths.Length > 0 ? widths[0] : 1f;
                return new PdfCharacterInfo(characterCode, unicode, gid, measuredWidth);
            }
            else if (unicode?.Length > 0)
            {
                using SKFont skFont = GetSkFont();

                ushort[] gids = skFont.GetGlyphs(unicode);
                ushort extractedGid = gids.Length > 0 ? gids[0] : (ushort)0;
                float[] widths = skFont.GetGlyphWidths(unicode);
                float measuredWidth = widths.Length > 0 ? widths[0] : 1f;
                return new PdfCharacterInfo(characterCode, unicode, extractedGid, measuredWidth);
            }

            return new PdfCharacterInfo(characterCode, string.Empty, 0, 0f);
        }

        /// <summary>
        /// Creates and configures an SKFont for glyph measurement (size 1, subpixel, alias edging, no hinting).
        /// Only call when actual Skia measurement is needed.
        /// </summary>
        /// <returns>Configured SKFont instance.</returns>
        private SKFont GetSkFont()
        {
            SKTypeface typeface = Dictionary.Document.FontCache.GetTypeface(this);
            SKFont skFont = new SKFont(typeface, size: 1f)
            {
                Subpixel = true,
                LinearMetrics = true,
                Hinting = SKFontHinting.Normal,
                Edging = SKFontEdging.SubpixelAntialias,

            };
            return skFont;
        }

        /// <summary>
        /// Load ToUnicode CMap (heavy operation - lazy loaded using GetPageObject)
        /// </summary>
        private PdfCMap LoadToUnicodeCMap()
        {
            // Use GetPageObject instead of storing reference
            var toUnicodeObj = Dictionary.GetPageObject(PdfTokens.ToUnicodeKey);
            if (toUnicodeObj == null)
                return null;

            var cmapData = Document.StreamDecoder.DecodeContentStream(toUnicodeObj);
            var cMapContent = new PdfParseContext(cmapData);
            return PdfCMapParser.ParseCMapFromContext(ref cMapContent, Document);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }
                disposedValue = true;
            }
        }

        ~PdfFontBase()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}