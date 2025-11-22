using PdfReader.Color.Paint;
using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Collections.Concurrent;

namespace PdfReader.Fonts.Types;

/// <summary>
/// Base class for all PDF font types with common properties and interface
/// Provides the foundation for the proper font hierarchy according to PDF specification
/// Essential properties are read-only and set through constructor for immutability
/// Heavy operations are lazy-loaded using thread-safe Lazy&lt;T&gt; pattern
/// </summary>
public abstract class PdfFontBase : IDisposable
{
    private readonly ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo> _characterInfoCache = new ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo>();

    /// <summary>
    /// Constructor for all PDF fonts with essential immutable properties
    /// Performs only lightweight dictionary operations
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    protected PdfFontBase(PdfDictionary fontDictionary)
    {
        Dictionary = fontDictionary ?? throw new ArgumentNullException(nameof(fontDictionary));

        // Parse essential properties from the font object (lightweight operations)
        Type = fontDictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();
        BaseFont = fontDictionary.GetString(PdfTokens.BaseFontKey);
        ToUnicodeCMap = LoadToUnicodeCMap();
        FontDescriptor = PdfFontDescriptor.FromDictionary(fontDictionary.GetDictionary(PdfTokens.FontDescriptorKey));
    }

    /// <summary>
    /// Returns the SkiaSharp SKTypeface instance for this PDF font.
    /// </summary>
    internal protected abstract SKTypeface Typeface { get; }

    /// <summary>
    /// Returns a SkiaSharp SKFont instance for this PDF font.
    /// </summary>
    /// <returns>SKFont instance.</returns>
    public SKFont GetSkiaFont()
    {
        return PdfPaintFactory.CreateTextFont(Typeface);
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
    /// Base font name (PostScript name)
    /// </summary>
    public PdfString BaseFont { get; }
    
    /// <summary>
    /// PDF document containing this font (convenience property)
    /// </summary>
    public PdfDocument Document => Dictionary.Document;
    
    /// <summary>
    /// Loaded ToUnicode CMap for character-to-Unicode mapping.
    /// </summary>
    public PdfCMap ToUnicodeCMap { get; }

    /// <summary>
    /// Check if this font has embedded font data
    /// </summary>
    public abstract bool IsEmbedded { get; }
    
    /// <summary>
    /// Get the font descriptor (contains metrics and embedding info)
    /// May be direct or inherited from descendant fonts
    /// Implementation may use lazy loading
    /// </summary>
    public virtual PdfFontDescriptor FontDescriptor { get; }
    
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
    public virtual string GetUnicodeString(PdfCharacterCode code)
    {
        if (ToUnicodeCMap != null)
        {
            var unicode = ToUnicodeCMap.GetUnicode(code);
            if (unicode != null)
            {
                return unicode;
            }
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
            using SKFont skFont = GetSkiaFont();
            float[] widths = skFont.GetGlyphWidths([gid]);
            float measuredWidth = widths.Length > 0 ? widths[0] : 1f;
            return new PdfCharacterInfo(characterCode, unicode, gid, measuredWidth);
        }
        else if (unicode?.Length > 0) // last resort: try to get GID and width from Skia
        {
            using SKFont skFont = GetSkiaFont();

            ushort[] gids = skFont.GetGlyphs(unicode);
            ushort extractedGid = gids.Length > 0 ? gids[0] : (ushort)0;
            float[] widths = skFont.GetGlyphWidths(unicode);
            float measuredWidth = widths.Length > 0 ? widths[0] : 1f;
            return new PdfCharacterInfo(characterCode, unicode, extractedGid, measuredWidth);
        }

        return new PdfCharacterInfo(characterCode, string.Empty, 0, 0f);
    }

    /// <summary>
    /// Load ToUnicode CMap (heavy operation - lazy loaded using GetPageObject)
    /// </summary>
    private PdfCMap LoadToUnicodeCMap()
    {
        // Use GetPageObject instead of storing reference
        var toUnicodeObj = Dictionary.GetObject(PdfTokens.ToUnicodeKey);
        if (toUnicodeObj == null)
            return null;

        var cmapData = toUnicodeObj.DecodeAsMemory();
        return PdfCMapParser.ParseCMap(cmapData, Document);
    }

    protected virtual void Dispose(bool disposing)
    {
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