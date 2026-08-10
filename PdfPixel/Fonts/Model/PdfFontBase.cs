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
    /// <see langword="true"/> when this font declares (or derives) explicit per-code advance widths,
    /// as distinct from relying entirely on a substitute typeface's own metrics.
    /// </summary>
    protected internal abstract bool HasWidths { get; }

    /// <summary>
    /// <see langword="true"/> when a substitute glyph's own shaped width should be rescaled to match this
    /// font's declared advance width, rather than trusted as-is. Skipped when the substitute resolves to a
    /// Standard 14 family, since its metrics are already trusted to match.
    /// </summary>
    protected internal bool ShouldRescale
    {
        get
        {
            return IsSubstitutedFont
                && HasWidths
                && !SubstitutionInfo.GetStandardName().HasValue;
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
    /// Get the width of a character/glyph, or <see langword="null"/> when this font defines none for
    /// the code - a width the font defines as zero is a width, not an absent one.
    /// Implementation varies by font type
    /// </summary>
    public abstract float? GetWidth(PdfCharacterCode code);

    /// <summary>
    /// Returns the vertical displacement vector for the specified character code.
    /// </summary>
    /// <param name="code"></param>
    public abstract PdfVerticalMetric GetVerticalDisplacement(PdfCharacterCode code);

    /// <summary>
    /// Resolves the glyphs <paramref name="characterCode"/> draws as, together with the typeface they
    /// belong to. Implemented per font type, since what a character code means - and therefore how a
    /// glyph is found for it - is a property of that font type's code space alone.
    /// </summary>
    /// <param name="characterCode">The character code to resolve glyphs for.</param>
    /// <param name="renderingUnicode">The Unicode text the code stands for, for font types that substitute by Unicode.</param>
    protected abstract PdfGlyphResolution ResolveGlyphs(PdfCharacterCode characterCode, string? renderingUnicode);

    /// <summary>
    /// Resolves glyphs from this font's own program: the mapping the font defines for the code, and
    /// failing that the program's own "cmap" for the character the code stands for. Identical for every
    /// font type - the mapping itself is what differs, and each supplies that through
    /// <see cref="GetGid"/> - so the step is shared. Empty when no font program is available.
    /// </summary>
    /// <param name="characterCode">The character code to resolve glyphs for.</param>
    /// <param name="renderingUnicode">The Unicode text the code stands for.</param>
    protected PdfGlyphResolution ResolveFromFontProgram(PdfCharacterCode characterCode, string? renderingUnicode)
    {
        IPdfTypeface? typeface = Typeface;

        if (IsSubstitutedFont || typeface == null)
        {
            return default;
        }

        ushort? gid = GetGid(characterCode);

        if (gid != null)
        {
            return new PdfGlyphResolution(typeface, [gid], isMappedByFont: true);
        }

        // The program is embedded but maps nothing to this code; its own "cmap" is still the best
        // source for the character the code stands for.
        return (renderingUnicode?.Length > 0)
            ? new PdfGlyphResolution(typeface, typeface.GetGlyphs(renderingUnicode), isMappedByFont: false)
            : default;
    }

    /// <summary>
    /// Resolves glyphs for the Unicode a character code stands for against a substitute typeface.
    /// Identical for every font type that reaches this point - once a code has been reduced to Unicode,
    /// nothing about the original code space is left to distinguish them - so the step is shared, while
    /// deciding whether to reach it at all stays with each <see cref="ResolveGlyphs"/> implementation.
    /// </summary>
    /// <param name="renderingUnicode">The Unicode text to shape against a substitute.</param>
    protected PdfGlyphResolution SubstituteByUnicode(string? renderingUnicode)
    {
        if (renderingUnicode == null || renderingUnicode.Length == 0)
        {
            return default;
        }

        IPdfTypeface substitute = Document.FontProvider.GetTypefaceByUnicode(SubstitutionInfo, renderingUnicode);
        return new PdfGlyphResolution(substitute, substitute.GetGlyphs(renderingUnicode), isMappedByFont: false);
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
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID (GID) for the character code, or <see langword="null"/> if not found.</returns>
    public abstract ushort? GetGid(PdfCharacterCode code);

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
    /// Turns the glyphs <see cref="ResolveGlyphs"/> found for a character code into its full rendering
    /// information, applying the metrics this font declares for the code to whatever typeface supplied
    /// those glyphs.
    /// </summary>
    /// <param name="characterCode">The character code to extract info for.</param>
    /// <returns>Resolved character info including Unicode, GIDs, and widths.</returns>
    protected PdfCharacterInfo ExtractCharacterInfoCore(PdfCharacterCode characterCode)
    {
        string? renderingUnicode = GetRenderingUnicodeString(characterCode);
        PdfGlyphResolution resolution = ResolveGlyphs(characterCode, renderingUnicode);
        string? unicode = GetUnicodeString(characterCode);

        if (resolution.Typeface == null || resolution.GlyphIds == null)
        {
            IPdfTypeface fallbackTypeface = Document.FontProvider.GetFallbackTypeface(SubstitutionInfo);
            return new PdfCharacterInfo(characterCode, fallbackTypeface, string.Empty, [null], 0, [0], 1, PdfPoint.Empty, default);
        }

        IPdfTypeface typeface = resolution.Typeface;
        ushort?[] glyphIds = resolution.GlyphIds;
        float? declaredWidth = GetWidth(characterCode);
        PdfVerticalMetric displacement = GetVerticalDisplacement(characterCode);
        bool shouldRescale = ShouldRescale;

        // A glyph the font mapped itself is described by the font's own advance width; a substitute's
        // glyph has to be measured, since nothing ties its advance to the width declared here.
        float[] widths;
        if (declaredWidth.HasValue && resolution.IsMappedByFont && !shouldRescale)
        {
            widths = new float[] { declaredWidth.Value };
        }
        else
        {
            widths = typeface.GetWidths(glyphIds);
        }

        float originalWidth = declaredWidth ?? widths.Sum();

        (float xScale, PdfPoint origin, float advancement) = GetScalingAndOrigin(renderingUnicode, displacement, originalWidth, widths, shouldRescale);

        return new PdfCharacterInfo(characterCode, typeface, unicode, glyphIds, originalWidth, widths, xScale, origin, advancement);
    }

    private (float xScale, PdfPoint Origin, float Advancement) GetScalingAndOrigin(string? unicode, in PdfVerticalMetric verticalMetric, float originalWidth, float[] widths, bool shouldRescale)
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
            bool isLetterOrDigit = unicode?.Length > 0 && char.IsLetterOrDigit(unicode[0]);

            if (shouldRescale && isLetterOrDigit && totalWidth > originalWidth)
            {
                // Shaped glyphs wider than the declared advance are condensed to fit it.
                xScale = originalWidth / totalWidth;
                offsetX = 0;
            }
            else if (!shouldRescale && isLetterOrDigit)
            {
                // The shaped glyphs carry their own trusted metrics, so they are left where they fall.
                xScale = 1;
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
