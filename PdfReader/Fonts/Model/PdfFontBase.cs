using PdfReader.Color.Paint;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Base class for all PDF font types with common properties and interface.
/// </summary>
public abstract class PdfFontBase : IDisposable
{
    private readonly ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo> _characterInfoCache = new ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo>();

    /// <summary>
    /// Constructor for all PDF fonts with essential immutable properties
    /// Performs only lightweight dictionary operations
    /// </summary>
    /// <param name="fontObject">PDF object containing the font definition</param>
    protected PdfFontBase(PdfObject fontObject)
    {
        FontObject = fontObject ?? throw new ArgumentNullException(nameof(fontObject));
        Dictionary = fontObject.Dictionary ?? throw new ArgumentNullException(nameof(fontObject));

        // Parse essential properties from the font object (lightweight operations)
        Type = Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();
        BaseFont = Dictionary.GetString(PdfTokens.BaseFontKey);
        ToUnicodeCMap = LoadToUnicodeCMap();
        FontDescriptor = PdfFontDescriptor.FromDictionary(Dictionary.GetDictionary(PdfTokens.FontDescriptorKey));
        SubstitutionInfo = PdfSubstitutionInfo.Parse(BaseFont, FontDescriptor);
    }

    /// <summary>
    /// Returns the SkiaSharp SKTypeface instance for this PDF font.
    /// </summary>
    internal protected abstract SKTypeface Typeface { get; }

    /// <summary>
    /// Writing mode for this font's CMap (horizontal/vertical).
    /// </summary>
    internal protected virtual CMapWMode WritingMode { get; } = CMapWMode.Horizontal;

    /// <summary>
    /// Information required for font substitution.
    /// </summary>
    internal protected virtual PdfSubstitutionInfo SubstitutionInfo { get; }

    internal protected bool SubstituteFont => Typeface == null;

    /// <summary>
    /// Returns the SkiaSharp SKTypeface instance for this PDF font.
    /// </summary>
    /// <param name="unicode">Hint for font substitution.</param>
    /// <returns>SKTypeface instance, should not be disposed.</returns>
    internal SKTypeface GetTypeface(string unicode)
    {
        if (Typeface != null)
        {
            return Typeface;
        }
        else
        {
            return Document.FontSubstitutor.SubstituteTypeface(SubstitutionInfo, unicode);
        }
    }

    /// <summary>
    /// Original PDF font object.
    /// </summary>
    public PdfObject FontObject { get; }

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
    /// Returns the vertical displacement vector for the specified character code.
    /// </summary>
    /// <param name="code"></param>
    public abstract VerticalMetric GetVerticalDisplacement(PdfCharacterCode code);

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
        var displacement = GetVerticalDisplacement(characterCode);
        var typeface = GetTypeface(unicode);

        if (gid != 0 && width != 0)
        {
            float[] widths = [width];
            (float xScale, SKPoint origin, float advancement) = GetScalingAndOrigin(unicode, default, displacement, width, widths);
            return new PdfCharacterInfo(characterCode, typeface, unicode, [gid], width, widths, xScale, origin, advancement);
        }
        else if (gid != 0 && unicode?.Length > 0)
        {
            using SKFont skFont = PdfPaintFactory.CreateTextFont(typeface);
            width = skFont.GetGlyphWidths(unicode).Sum();
            float[] widths = [width];
            (float xScale, SKPoint origin, float advacement) = GetScalingAndOrigin(unicode, default, displacement, width, widths);

            return new PdfCharacterInfo(characterCode, typeface, unicode, [gid], width, widths, xScale, origin, advacement);
        }
        else if (gid == 0 && width != 0 && unicode?.Length > 0)
        {
            using SKFont skFont = PdfPaintFactory.CreateTextFont(typeface);
            ushort[] gids = skFont.GetGlyphs(unicode);
            float[] widths = skFont.GetGlyphWidths(unicode);
            (float xScale, SKPoint origin, float advacement) = GetScalingAndOrigin(unicode, skFont, displacement, width, widths);

            return new PdfCharacterInfo(characterCode, typeface, unicode, gids, width, widths, xScale, origin, advacement);
        }
        else if (unicode?.Length > 0) // last resort: try to get both GID and width from Skia
        {
            using SKFont skFont = PdfPaintFactory.CreateTextFont(typeface);
            ushort[] gids = skFont.GetGlyphs(unicode);
            float[] widths = skFont.GetGlyphWidths(unicode);
            width = widths.Sum();
            (float xScale, SKPoint origin, float advacement) = GetScalingAndOrigin(unicode, skFont, displacement, width, widths);

            return new PdfCharacterInfo(characterCode, typeface, unicode, gids, width, widths, xScale, origin, advacement);
        }

        return new PdfCharacterInfo(characterCode, typeface, string.Empty, [0], 0, [0], 1, SKPoint.Empty, default);
    }

    private (float xScale, SKPoint Origin, float Advancement) GetScalingAndOrigin(string unicode, SKFont font, VerticalMetric verticalMetric, float originalWidth, float[] widths)
    {
        float totalWidth = widths.Sum();
        float xScale;
        float offsetX;
        float offsetY = 0;
        float advancement;

        if (WritingMode == CMapWMode.Vertical)
        {
            offsetX = -(verticalMetric.V1X ?? totalWidth / 2f);
            offsetY += verticalMetric.V1;
            xScale = 1;
            advancement = verticalMetric.W1;
        }
        else
        {
            if (unicode?.Length > 0 && char.IsLetterOrDigit(unicode[0]))
            {
                xScale = originalWidth / totalWidth;
                offsetX = 0;
            }
            else
            {
                // Center the shaped glyphs within the OriginalWidth block when they differ.
                offsetX = (originalWidth - totalWidth) / 2f;
                xScale = 1;
            }

            advancement = originalWidth;
        }

        if (FontDescriptor != null && font != null)
        {
            offsetY += FontDescriptor.Descent / 1000f + font.Metrics.Descent;
        }

        return (xScale, new SKPoint(offsetX, offsetY), advancement);
    }

    /// <summary>
    /// Load ToUnicode CMap (heavy operation - lazy loaded using GetPageObject)
    /// </summary>
    private PdfCMap LoadToUnicodeCMap()
    {
        // Use GetPageObject instead of storing reference
        var toUnicodeObj = Dictionary.GetObject(PdfTokens.ToUnicodeKey);

        if (toUnicodeObj == null)
        {
            return null;
        }

        if (toUnicodeObj.Reference.IsValid && Document.CMapCache.CMapStreams.TryGetValue(toUnicodeObj.Reference, out var cachedCMap))
        {
            return cachedCMap;
        }

        var cmapData = toUnicodeObj.DecodeAsMemory();

        var parsedCMap = PdfCMapParser.ParseCMap(cmapData, Document);

        if (toUnicodeObj.Reference.IsValid)
        {
            Document.CMapCache.CMapStreams[toUnicodeObj.Reference] = parsedCMap;
        }

        return parsedCMap;
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