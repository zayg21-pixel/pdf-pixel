using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Sfnt;
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
    private readonly float[]? _widths;

    /// <summary>
    /// Constructor for CID fonts - lightweight operations only
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

        if (_typeface is CffPdfTypeface cffPdfTypeface)
        {
            CffFont cffFont = cffPdfTypeface.CffTypeface.Fonts[0];

            if (CidToGidMap == null)
            {
                _widths = BuildGidWidths(cffPdfTypeface, cffFont.Characters.Length);
                CidToGidMap = PdfCidToGidMap.FromCffFont(cffFont);
            }
        }
        else if (_typeface is SfntPdfTypeface sfntPdfTypeface)
        {
            CffTypeface? cffTypeface = sfntPdfTypeface.SfntFont.CffTypeface;
            if (cffTypeface != null)
            {
                CffFont cffFont = cffTypeface.Fonts[0];

                if (CidToGidMap == null)
                {
                    _widths = BuildGidWidths(sfntPdfTypeface, cffFont.Characters.Length);
                    CidToGidMap = PdfCidToGidMap.FromCffFont(cffFont);
                }
            }
            else if (FontDescriptor != null && Widths.CidWidths.Count == 0 && Widths.DefaultWidth == null)
            {
                int glyphCount = sfntPdfTypeface.SfntFont.Maxp?.NumGlyphs ?? 0;
                _widths = BuildGidWidths(sfntPdfTypeface, glyphCount);
            }
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
    /// Returns explicit width if defined, otherwise DefaultWidth, otherwise 0f.
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
    /// Get character width for a given character code. A CID font always defines one: the "W" entry
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
    /// Convert Character ID (CID) to Glyph ID (GID) for font rendering.
    /// Uses lazy-loaded CIDToGIDMap, or returns <see langword="null"/> if no mapping exists.
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

        if (CidToGidMap.HasMapping(cid))
        {
            return CidToGidMap.GetGID(cid);
        }

        return null;
    }

    private IPdfTypeface? GetTypeface()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                // A CID-keyed descendant font should never declare /FontFile (Type1) per spec - only
                // /FontFile3 /CIDFontType0C is valid here. Some producers use /FontFile anyway for a
                // bare CFF program; GetTypefaceFromCff validates that itself and throws (caught below)
                // if the bytes genuinely aren't CFF, so no separate check is needed here.
                case PdfFontFileFormat.CIDFontType0C:
                case PdfFontFileFormat.Type1:
                {
                    ReadOnlyMemory<byte> cffBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
                    PdfTypefaceLoader loader = new(Type, Document.LoggerFactory);
                    return loader.GetTypefaceFromCff(cffBytes);
                }
                case PdfFontFileFormat.OpenType:
                case PdfFontFileFormat.TrueType:
                {
                    PdfTypefaceLoader loader = new(Type, Document.LoggerFactory);
                    return loader.GetTypefaceFromSfnt(FontDescriptor.FontFileStream);
                }
            }

            if (FontDescriptor?.FontFileStream != null)
            {
                _logger.LogWarning(
                    "Unsupported embedded font format '{Format}' for font '{FontName}', will attempt substitution",
                    FontDescriptor.FontFileFormat,
                    BaseFont);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        return null;
    }

    private static float[] BuildGidWidths(IPdfTypeface typeface, int glyphCount)
    {
        var gidWidths = new float[glyphCount];
        for (int gid = 0; gid < glyphCount; gid++)
        {
            gidWidths[gid] = typeface.GetWidth((ushort)gid);
        }

        return gidWidths;
    }

    private PdfCidSystemInfo? LoadCidSystemInfo()
    {
        PdfDictionary? cidSystemInfoDict = Dictionary.GetDictionary(PdfTokens.CidSystemInfoKey);
        return PdfCidSystemInfo.FromDictionary(cidSystemInfoDict);
    }

    private PdfCidToGidMap? LoadCidToGidMap()
    {
        // Check if CIDToGIDMap is specified as "Identity" in the font dictionary
        PdfString cidToGidName = Dictionary.GetName(PdfTokens.CidToGidMapKey);
        if (cidToGidName == PdfTokens.IdentityKey)
        {
            return PdfCidToGidMap.CreateIdentityMapping();
        }

        // Use GetPageObject instead of stored reference
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _typeface?.Dispose();
        }
    }
}
