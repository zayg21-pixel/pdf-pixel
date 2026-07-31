using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Base class for all PDF font types with common properties and interface.
/// </summary>
public abstract class PdfFontBase : IDisposable
{
    private readonly ConcurrentDictionary<PdfCharacterCode, PdfCharacterInfo> _characterInfoCache = [];

    /// <summary>
    /// Constructor for all PDF fonts with essential immutable properties
    /// Performs only lightweight dictionary operations
    /// </summary>
    /// <param name="fontObject">PDF object containing the font definition</param>
    protected PdfFontBase(PdfObject fontObject)
    {
        FontObject = fontObject ?? throw new ArgumentNullException(nameof(fontObject));
        Dictionary = fontObject.Dictionary ?? throw new ArgumentNullException(nameof(fontObject));

        Type = Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfFontSubType>();
        BaseFont = Dictionary.GetString(PdfTokens.BaseFontKey);
        ToUnicodeCMap = LoadToUnicodeCMap();
        FontDescriptor = PdfFontDescriptor.FromDictionary(Dictionary.GetDictionary(PdfTokens.FontDescriptorKey));
        SubstitutionInfo = PdfSubstitutionInfo.Parse(BaseFont, FontDescriptor);
    }

    /// <summary>
    /// Returns the typeface for this PDF font.
    /// </summary>
    protected internal abstract IPdfTypeface? Typeface { get; }

    /// <summary>
    /// Writing mode for this font's CMap (horizontal/vertical).
    /// </summary>
    protected internal virtual CMapWMode WritingMode { get; } = CMapWMode.Horizontal;

    /// <summary>
    /// Information required for font substitution.
    /// </summary>
    protected internal virtual PdfSubstitutionInfo SubstitutionInfo { get; }

    /// <summary>
    /// <see langword="true"/> when glyphs of this font may be shaped against more than one substitute typeface,
    /// so rendering must group and switch fonts per glyph instead of assuming a single shared typeface.
    /// </summary>
    protected internal virtual bool IsSubstitutedFont => Typeface == null;

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
    internal IPdfDocumentInternal Document => Dictionary.Document;

    /// <summary>
    /// Loaded ToUnicode CMap for character-to-Unicode mapping.
    /// </summary>
    public PdfCMap? ToUnicodeCMap { get; }

    /// <summary>
    /// Get the font descriptor (contains metrics and embedding info)
    /// May be direct or inherited from descendant fonts
    /// Implementation may use lazy loading
    /// </summary>
    public virtual PdfFontDescriptor? FontDescriptor { get; }

    /// <summary>
    /// Get the width of a character/glyph
    /// Implementation varies by font type
    /// </summary>
    public abstract float GetWidth(PdfCharacterCode code);

    /// <summary>
    /// Returns the vertical displacement vector for the specified character code.
    /// </summary>
    /// <param name="code"></param>
    public abstract PdfVerticalMetric GetVerticalDisplacement(PdfCharacterCode code);

    /// <summary>
    /// Returns the typeface for this PDF font.
    /// When <see cref="IsSubstitutedFont"/> is <see langword="true"/>, re-resolves per glyph using the
    /// <paramref name="unicode"/>/<paramref name="width"/> hints instead of trusting a single fixed typeface,
    /// so glyphs missing from the primary substitute can still fall back to a different font.
    /// </summary>
    /// <param name="unicode">Hint for font substitution.</param>
    /// <param name="width">Optional horizontal scale hint (1.0 = normal). Mapped to the <c>wdth</c> axis when available.</param>
    /// <returns>A <see cref="IPdfTypeface"/> instance.</returns>
    internal IPdfTypeface GetTypeface(string? unicode, float? width)
    {
        if (!IsSubstitutedFont && Typeface != null)
        {
            return Typeface;
        }

        return Document.FontProvider.GetTypeface(SubstitutionInfo, unicode, width);
    }

    /// <summary>
    /// Converts a <see cref="PdfCharacterCode"/> to its corresponding Unicode string representation.
    /// </summary>
    /// <param name="code">The <see cref="PdfCharacterCode"/> to be converted. Cannot be <see langword="null"/>.</param>
    /// <returns>The Unicode string representation of the specified <see cref="PdfCharacterCode"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="code"/> is <see langword="null"/>.</exception>
    public virtual string? GetUnicodeString(PdfCharacterCode code)
    {
        if (ToUnicodeCMap != null)
        {
            return ToUnicodeCMap?.GetUnicode(code);
        }

        return null;
    }

    /// <summary>
    /// Converts a character code to the Unicode string used to select and shape a substitute glyph
    /// when the font has no direct code-to-GID mapping of its own. Never consults the font's
    /// /ToUnicode CMap.
    /// </summary>
    /// <param name="code">The character code to convert.</param>
    /// <returns>The Unicode string to shape against, or <see langword="null"/> if none is available.</returns>
    public virtual string? GetRenderingUnicodeString(PdfCharacterCode code) => GetUnicodeString(code);

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
        string? unicode = GetUnicodeString(characterCode);
        string? renderingUnicode = GetRenderingUnicodeString(characterCode);
        PdfVerticalMetric displacement = GetVerticalDisplacement(characterCode);

        if (gid != 0 && width != 0)
        {
            IPdfTypeface typeface = GetTypeface(renderingUnicode, width);
            float[] widths = [width];
            (float xScale, PdfPoint origin, float advancement) = GetScalingAndOrigin(renderingUnicode, displacement, width, widths);
            return new PdfCharacterInfo(characterCode, typeface, unicode, [gid], width, widths, xScale, origin, advancement);
        }
        else if (gid != 0 && renderingUnicode?.Length > 0)
        {
            IPdfTypeface typeface = GetTypeface(renderingUnicode, width);
            width = typeface.GetWidth(renderingUnicode);
            float[] widths = [width];
            (float xScale, PdfPoint origin, float advacement) = GetScalingAndOrigin(renderingUnicode, displacement, width, widths);

            return new PdfCharacterInfo(characterCode, typeface, unicode, [gid], width, widths, xScale, origin, advacement);
        }
        else if (gid == 0 && width != 0 && renderingUnicode?.Length > 0)
        {
            IPdfTypeface typeface = GetTypeface(renderingUnicode, width);
            ushort[] gids = typeface.GetGlyphs(renderingUnicode);
            float[] widths = typeface.GetWidths(gids);
            (float xScale, PdfPoint origin, float advacement) = GetScalingAndOrigin(renderingUnicode, displacement, width, widths);

            return new PdfCharacterInfo(characterCode, typeface, unicode, gids, width, widths, xScale, origin, advacement);
        }
        else if (renderingUnicode?.Length > 0)
        {
            IPdfTypeface typeface = GetTypeface(renderingUnicode, width);
            ushort[] gids = typeface.GetGlyphs(renderingUnicode);
            float[] widths = typeface.GetWidths(gids);
            width = widths.Sum();
            (float xScale, PdfPoint origin, float advacement) = GetScalingAndOrigin(renderingUnicode, displacement, width, widths);

            return new PdfCharacterInfo(characterCode, typeface, unicode, gids, width, widths, xScale, origin, advacement);
        }

        IPdfTypeface fallbackTypeface = GetTypeface(null, null);
        return new PdfCharacterInfo(characterCode, fallbackTypeface, string.Empty, [0], 0, [0], 1, PdfPoint.Empty, default);
    }

    private (float xScale, PdfPoint Origin, float Advancement) GetScalingAndOrigin(string? unicode, in PdfVerticalMetric verticalMetric, float originalWidth, float[] widths)
    {
        float totalWidth = widths.Sum();
        float xScale;
        float offsetX;
        float offsetY;
        float advancement;

        if (WritingMode == CMapWMode.Vertical)
        {
            offsetX = -(verticalMetric.V1X ?? totalWidth / 2f);
            offsetY = verticalMetric.V1;
            xScale = 1;
            advancement = verticalMetric.W1;
        }
        else
        {
            if (unicode?.Length > 0 && char.IsLetterOrDigit(unicode[0]))
            {
                // A zero totalWidth means the shaped glyphs carry no advance of their own; any xScale
                // multiplies out to the same zero-width result, so scale is safely clamped to 1.
                xScale = (totalWidth != 0) ? originalWidth / totalWidth : 1;
                offsetX = 0;
            }
            else
            {
                // Center the shaped glyphs within the OriginalWidth block when they differ.
                offsetX = (originalWidth - totalWidth) / 2f;
                xScale = 1;
            }

            offsetY = 0;
            advancement = originalWidth;
        }

        return (xScale, new PdfPoint(offsetX, offsetY), advancement);
    }

    /// <summary>
    /// Load ToUnicode CMap (heavy operation - lazy loaded using GetPageObject)
    /// </summary>
    private PdfCMap? LoadToUnicodeCMap()
    {
        // Use GetPageObject instead of storing reference
        PdfObject? toUnicodeObj = Dictionary.GetObject(PdfTokens.ToUnicodeKey);

        if (toUnicodeObj == null || !toUnicodeObj.HasStream)
        {
            return null;
        }

        if (toUnicodeObj.Reference.IsValid && Document.CMapCache.CMapStreams.TryGetValue(toUnicodeObj.Reference, out PdfCMap? cachedCMap))
        {
            return cachedCMap;
        }

        ReadOnlyMemory<byte> cmapData = toUnicodeObj.DecodeAsMemory();

        PdfCMap? parsedCMap = PdfCMapParser.ParseCMap(cmapData, Document);

        if (parsedCMap != null && toUnicodeObj.Reference.IsValid)
        {
            Document.CMapCache.CMapStreams[toUnicodeObj.Reference] = parsedCMap;
        }

        return parsedCMap;
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <inheritdoc/>
    ~PdfFontBase() => Dispose(disposing: false);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
