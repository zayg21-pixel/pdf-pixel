using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Typeface;
using PdfPixel.Models;
using PdfPixel.Text;
using System;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// CID fonts: CIDFontType0, CIDFontType2
/// Contains actual font data and glyph mappings for multi-byte character support
/// Used as descendant fonts in Type0 composite fonts
/// </summary>
public class PdfCidFont : PdfFontBase
{
    private readonly ILogger<PdfCidFont> _logger;
    private readonly IPdfTypeface? _typeface;

    /// <summary>
    /// Constructor for CID fonts. Parses the widths, vertical metrics, CID system info and
    /// /CIDToGIDMap, and loads the embedded font program.
    /// </summary>
    /// <param name="fontObject">PDF object containing the font definition</param>
    internal PdfCidFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfCidFont>();
        Widths = PdfCidFontWidths.Parse(Dictionary);
        VerticalMetrics = PdfCidFontVerticalMetrics.Parse(Dictionary);
        CidSystemInfo = LoadCidSystemInfo();
        CidToGidMap = LoadCidToGidMap();
        _typeface = GetTypeface();

        CffFont? embeddedCffFont = GetEmbeddedCffFont(_typeface);
        if (embeddedCffFont != null)
        {
            CidToGidMap = PdfCidToGidMap.FromCffFont(embeddedCffFont, CidToGidMap);
        }
    }

    /// <summary>
    /// The embedded or substituted typeface for this CID font.
    /// May be <see langword="null"/> when no embedded font data is present and no substitution has been applied.
    /// </summary>
    protected internal override IPdfTypeface? Typeface => _typeface;

    /// <summary>
    /// CID system information (Registry, Ordering, Supplement)
    /// </summary>
    public PdfCidSystemInfo? CidSystemInfo { get; }

    /// <summary>
    /// Character width information for CID-based characters
    /// Initialized during construction
    /// </summary>
    public PdfCidFontWidths Widths { get; }

    /// <inheritdoc/>
    protected internal override bool HasWidths => Widths.HasWidths;

    /// <summary>
    /// Gets the vertical metrics for the CID font.
    /// </summary>
    public PdfCidFontVerticalMetrics VerticalMetrics { get; }

    /// <summary>
    /// Loaded CID-to-GID mapping.
    /// </summary>
    public PdfCidToGidMap? CidToGidMap { get; }

    /// <summary>
    /// Gets the width for a given CID in this CID font.
    /// Returns the explicit width if defined, otherwise <c>/DW</c>, otherwise the 1000 glyph space
    /// units the specification defaults <c>/DW</c> to.
    /// </summary>
    /// <param name="cid">The CID to get the width for.</param>
    /// <returns>The width for the CID.</returns>
    public float GetWidthByCid(uint cid)
    {
        float? width = Widths.GetWidth(cid);
        return width ?? Widths.DefaultWidth ?? 1;
    }

    /// <summary>
    /// Returns the vertical displacement metrics for the specified CID, falling back to the font defaults when no per-CID entry is present.
    /// </summary>
    /// <param name="cid">The CID for which to retrieve vertical metrics.</param>
    /// <returns>The <see cref="PdfVerticalMetric"/> for the given CID.</returns>
    public PdfVerticalMetric GetVerticalDisplacementByCid(uint cid) => VerticalMetrics.GetMetrics(cid);

    /// <summary>
    /// Gets the character width for a given character code. A CID font always defines one: the "W" entry
    /// for the CID, the font's "DW", or the default the specification gives "DW".
    /// </summary>
    public override float? GetWidth(PdfCharacterCode code) => GetWidthByCid((uint)code);

    /// <summary>
    /// Returns the vertical displacement metrics for the given character code, mapping it to a CID and delegating to <see cref="GetVerticalDisplacementByCid"/>.
    /// </summary>
    /// <param name="code">The character code to retrieve vertical metrics for.</param>
    /// <returns>The <see cref="PdfVerticalMetric"/> for the character code.</returns>
    public override PdfVerticalMetric GetVerticalDisplacement(PdfCharacterCode code) => VerticalMetrics.GetMetrics((uint)code);

    /// <summary>
    /// Converts a Character ID (CID) to a Glyph ID (GID) through <see cref="CidToGidMap"/>, or the CID
    /// itself when the font states no map. Returns <see langword="null"/> when the font is substituted.
    /// </summary>
    public ushort? GetGidByCid(uint cid)
    {
        // font is substituted, no mapping available
        if (Typeface == null)
        {
            return null;
        }

        if (CidToGidMap == null)
        {
            return (ushort)cid;
        }

        return CidToGidMap.GetGID(cid);
    }

    private IPdfTypeface? GetTypeface()
    {
        PdfFontDescriptor? fontDescriptor = FontDescriptor;

        if (fontDescriptor?.FontFileStream == null)
        {
            return null;
        }

        try
        {
            PdfTypefaceLoader loader = new(fontDescriptor, Type, Document.LoggerFactory);
            return loader.GetTypeface();
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        return null;
    }

    /// <summary>
    /// Returns the CFF font program the typeface is built on, either as a bare CFF or as the CFF table of an
    /// OpenType container. Returns <see langword="null"/> when the typeface has no CFF font program.
    /// </summary>
    private static CffFont? GetEmbeddedCffFont(IPdfTypeface? typeface)
    {
        CffTypeface? cffTypeface = null;

        if (typeface is CffPdfTypeface cffPdfTypeface)
        {
            cffTypeface = cffPdfTypeface.CffTypeface;
        }
        else if (typeface is SfntPdfTypeface sfntPdfTypeface)
        {
            cffTypeface = sfntPdfTypeface.SfntFont.CffTypeface;
        }

        if (cffTypeface == null || cffTypeface.Fonts.Length == 0)
        {
            return null;
        }

        return cffTypeface.Fonts[0];
    }

    private PdfCidSystemInfo? LoadCidSystemInfo()
    {
        PdfDictionary? cidSystemInfoDict = Dictionary.GetDictionary(PdfTokens.CidSystemInfoKey);
        return PdfCidSystemInfo.FromDictionary(cidSystemInfoDict);
    }

    private PdfCidToGidMap? LoadCidToGidMap()
    {
        // Check if CIDToGIDMap is specified as "Identity" in the font dictionary
        if (Dictionary.GetName(PdfTokens.CidToGidMapKey) == PdfTokens.IdentityKey)
        {
            return PdfCidToGidMap.CreateIdentityMapping();
        }

        PdfObject? cidToGidObj = Dictionary.GetObject(PdfTokens.CidToGidMapKey);
        if (cidToGidObj != null)
        {
            // Load as stream data
            ReadOnlyMemory<byte> cidToGidData = cidToGidObj.DecodeAsMemory();
            return PdfCidToGidMap.FromStreamData(cidToGidData);
        }

        return null;
    }

    /// <summary>
    /// Extracts character codes from raw bytes for CID fonts.
    /// Always uses fixed-length segmentation (2 bytes per CID).
    /// </summary>
    /// <param name="bytes">Raw bytes to extract character codes from.</param>
    /// <returns>Array of extracted PdfCharacterCode items, each representing a 2-byte CID.</returns>
    public override PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return Array.Empty<PdfCharacterCode>();
        }

        const int codeLength = 2;
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
    /// Gets the glyph ID (GID) for the specified character code in a CID font.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID (GID) for the character code, or <see langword="null"/> if not found.</returns>
    public override ushort? GetGid(PdfCharacterCode code)
    {
        if (code == null)
        {
            return null;
        }

        var cid = (uint)code;
        return GetGidByCid(cid);
    }

    /// <summary>
    /// Resolves glyphs for a CID through this font's CIDToGIDMap, falling back to the Unicode the code
    /// stands for once the embedded program is unavailable.
    /// </summary>
    protected override PdfGlyphResolution ResolveGlyphs(PdfCharacterCode characterCode, string? renderingUnicode)
    {
        PdfGlyphResolution resolution = ResolveFromFontProgram(characterCode, renderingUnicode);

        return (!resolution.IsEmpty) ? resolution : SubstituteByUnicode(renderingUnicode);
    }
}
