using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Cff;
using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System;

namespace PdfReader.Fonts.Model;

/// <summary>
/// CID fonts: CIDFontType0, CIDFontType2
/// Contains actual font data and glyph mappings for multi-byte character support
/// Used as descendant fonts in Type0 composite fonts
/// </summary>
public class PdfCidFont : PdfFontBase
{
    private readonly ILogger<PdfCidFont> _logger;
    private readonly SKTypeface _typeface;

    /// <summary>
    /// Constructor for CID fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    public PdfCidFont(PdfDictionary fontDictionary) : base(fontDictionary)
    {
        _logger = fontDictionary.Document.LoggerFactory.CreateLogger<PdfCidFont>();
        Widths = CidFontWidths.Parse(fontDictionary);
        VerticalMetrics = CidFontVerticalMetrics.Parse(fontDictionary);
        CidSystemInfo = LoadCidSystemInfo();
        CidToGidMap = LoadCidToGidMap();
        var typefaceInfo = GetTypeface();
        _typeface = typefaceInfo.Typeface;

        if (typefaceInfo.CffInfo != null && CidToGidMap == null)
        {
            CidToGidMap = PdfCidToGidMap.FromCffFont(typefaceInfo.CffInfo);
        }
    }

    internal protected override SKTypeface Typeface => _typeface;
    
    /// <summary>
    /// CID system information (Registry, Ordering, Supplement)
    /// </summary>
    public PdfCidSystemInfo CidSystemInfo { get; }

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
    public PdfCidToGidMap CidToGidMap { get; }

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

    public VerticalMetric GetVerticalDisplacementByCid(uint cid)
    {
        return VerticalMetrics.GetMetrics(cid);
    }

    /// <summary>
    /// Get character width for a given character code
    /// </summary>
    public override float GetWidth(PdfCharacterCode code)
    {
        return GetWidthByCid((uint)code);
    }

    public override VerticalMetric GetVerticalDisplacement(PdfCharacterCode code)
    {
        return VerticalMetrics.GetMetrics((uint)code);
    }

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

    private (SKTypeface Typeface, CffInfo CffInfo) GetTypeface()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                case PdfFontFileFormat.CIDFontType0C:
                {
                    var cffSidMapper = new CffSidGidMapper(Document.LoggerFactory);
                    var cffBytes = FontDescriptor.FontFileObject.DecodeAsMemory();

                    if (!cffSidMapper.TryParseNameKeyed(cffBytes, out var cffInfo))
                    {
                        _logger.LogWarning("Failed to parse embedded Type1C font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to parse embedded Type1C font data.");
                    }

                    var typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    var typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    return (typeface, cffInfo);
                }

                case PdfFontFileFormat.TrueType:
                {
                    var typeface = SKTypeface.FromStream(FontDescriptor.FontFileObject.DecodeAsStream());
                    return (typeface, null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }

        return default;
    }

    private PdfCidSystemInfo LoadCidSystemInfo()
    {
        var cidSystemInfoDict = Dictionary.GetDictionary(PdfTokens.CidSystemInfoKey);
        return PdfCidSystemInfo.FromDictionary(cidSystemInfoDict);
    }

    private PdfCidToGidMap LoadCidToGidMap()
    {
        // Check if CIDToGIDMap is specified as "Identity" in the font dictionary
        var cidToGidName = Dictionary.GetName(PdfTokens.CidToGidMapKey);
        if (cidToGidName == PdfTokens.IdentityKey)
        {
            return PdfCidToGidMap.CreateIdentityMapping();
        }

        // Use GetPageObject instead of stored reference
        var cidToGidObj = Dictionary.GetObject(PdfTokens.CidToGidMapKey);
        if (cidToGidObj != null)
        {
            // Load as stream data
            var cidToGidData = cidToGidObj.DecodeAsMemory();
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


    protected override void Dispose(bool disposing)
    {
        _typeface?.Dispose();
        base.Dispose(disposing);
    }
}