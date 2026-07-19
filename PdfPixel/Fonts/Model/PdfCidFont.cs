using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.TrueType;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
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
    private readonly SKTypeface _typeface;
    private readonly float[]? _widths;

    /// <summary>
    /// Constructor for CID fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF object containing the font definition</param>
    internal PdfCidFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfCidFont>();
        Widths = CidFontWidths.Parse(Dictionary);
        VerticalMetrics = CidFontVerticalMetrics.Parse(Dictionary);
        CidSystemInfo = LoadCidSystemInfo();
        CidToGidMap = LoadCidToGidMap();
        (SKTypeface Typeface, CffInfo? CffInfo) typefaceInfo = GetTypeface();
        _typeface = typefaceInfo.Typeface;

        if (typefaceInfo.CffInfo != null && CidToGidMap == null)
        {
            _widths = typefaceInfo.CffInfo.GidWidths;
            CidToGidMap = PdfCidToGidMap.FromCffFont(typefaceInfo.CffInfo);
        }

        if (FontDescriptor != null && Widths.CidWidths.Count == 0 && Widths.DefaultWidth == null)
        {
            SfntFontTables tables = SfntFontTablesParser.GetSfntFontTables(_typeface);
            _widths = tables.GidWidths;

        }
    }

    /// <summary>
    /// The embedded or substituted SkiaSharp typeface for this CID font.
    /// May be <see langword="null"/> when no embedded font data is present and no substitution has been applied.
    /// </summary>
    protected internal override SKTypeface Typeface => _typeface;

    /// <summary>
    /// CID system information (Registry, Ordering, Supplement)
    /// </summary>
    public PdfCidSystemInfo? CidSystemInfo { get; }

    /// <summary>
    /// Character width information for CID-based characters
    /// Initialized during construction
    /// </summary>
    public CidFontWidths Widths { get; }

    /// <summary>
    /// Gets the vertical metrics for the CID font.
    /// </summary>
    public CidFontVerticalMetrics VerticalMetrics { get; }

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
    /// <returns>The <see cref="VerticalMetric"/> for the given CID.</returns>
    public VerticalMetric GetVerticalDisplacementByCid(uint cid) => VerticalMetrics.GetMetrics(cid);

    /// <summary>
    /// Get character width for a given character code
    /// </summary>
    public override float GetWidth(PdfCharacterCode code) => GetWidthByCid((uint)code);

    /// <summary>
    /// Returns the vertical displacement metrics for the given character code, mapping it to a CID and delegating to <see cref="GetVerticalDisplacementByCid"/>.
    /// </summary>
    /// <param name="code">The character code to retrieve vertical metrics for.</param>
    /// <returns>The <see cref="VerticalMetric"/> for the character code.</returns>
    public override VerticalMetric GetVerticalDisplacement(PdfCharacterCode code) => VerticalMetrics.GetMetrics((uint)code);

    /// <summary>
    /// Convert Character ID (CID) to Glyph ID (GID) for font rendering.
    /// Uses lazy-loaded CIDToGIDMap or returns 0 if no mapping exists.
    /// </summary>
    public ushort GetGidByCid(uint cid)
    {
        // font is substituted, no mapping available
        if (Typeface == null)
        {
            return 0;
        }

        if (CidToGidMap == null)
        {
            return (ushort)cid;
        }

        if (CidToGidMap.HasMapping(cid))
        {
            return CidToGidMap.GetGID(cid);
        }

        return 0;
    }

    private (SKTypeface Typeface, CffInfo? CffInfo) GetTypeface()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                case PdfFontFileFormat.CIDFontType0C:
                {
                    ReadOnlyMemory<byte> cffBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
                    return LoadFromCffBytes(cffBytes);
                }
                case PdfFontFileFormat.OpenType:
                {
                    ReadOnlyMemory<byte> openTypeBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;

                    if (OpenTypeCffTableReader.TryExtractCffTable(openTypeBytes, out ReadOnlyMemory<byte> cffTableBytes))
                    {
                        return LoadFromCffBytes(cffTableBytes);
                    }

                    using SKData skFontData = SKData.CreateCopy(openTypeBytes.ToArray());
                    SKTypeface typeface = SKTypeface.FromData(skFontData);

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded OpenType font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded OpenType font data.");
                    }

                    return (typeface, null);
                }
                case PdfFontFileFormat.TrueType:
                {
                    SKTypeface typeface = SKTypeface.FromStream(FontDescriptor.FontFileStream?.DecodeAsStream());

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded TrueType font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded TrueType font data.");
                    }

                    return (typeface, null);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        return default;
    }

    /// <summary>
    /// Parses raw (unwrapped) CFF font data, rebuilds it into a minimal OpenType container, and loads it as
    /// the font's typeface. Shared by CIDFontType0C and CFF-flavored OpenType FontFile3 data.
    /// </summary>
    /// <param name="cffBytes">The raw CFF font program bytes.</param>
    private (SKTypeface Typeface, CffInfo? CffInfo) LoadFromCffBytes(in ReadOnlyMemory<byte> cffBytes)
    {
        CffSidGidMapper cffSidMapper = new(Document.LoggerFactory);

        if (!cffSidMapper.TryParseNameKeyed(cffBytes, out CffInfo? cffInfo))
        {
            _logger.LogWarning("Failed to parse embedded CFF font data for font '{FontName}'", BaseFont);
            throw new InvalidOperationException("Failed to parse embedded CFF font data.");
        }

        byte[]? typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
        SKTypeface typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

        if (typeface == null)
        {
            _logger.LogWarning("Failed to create typeface from embedded CFF font data for font '{FontName}'", BaseFont);
            throw new InvalidOperationException("Failed to create typeface from embedded CFF font data.");
        }

        return (typeface, cffInfo);
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

        var cid = (uint)code;
        return GetGidByCid(cid);
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _typeface?.Dispose();
        base.Dispose(disposing);
    }
}
