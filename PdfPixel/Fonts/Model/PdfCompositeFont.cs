using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Type0 (Composite) fonts: Multi-byte character support
/// Acts as a wrapper that delegates to descendant CID fonts for actual rendering
/// Handles character encoding and script/language coordination.
/// </summary>
public class PdfCompositeFont : PdfFontBase
{
    private readonly CMapWMode _writingMode;
    private readonly Dictionary<uint, string> _toUnicode;

    public PdfCompositeFont(PdfObject fontObject) : base(fontObject)
    {
        DescendantFonts = LoadDescendantFonts();
        (CodeToCidCMap, CMapName) = LoadCodeToCidCMap();
        _writingMode = CodeToCidCMap?.WMode ?? CMapWMode.Horizontal;
        _toUnicode = PdfToUnicodeMapProvider.GetToUnicodeMap(PrimaryDescendant?.CidSystemInfo);
    }

    public override PdfFontDescriptor FontDescriptor => PrimaryDescendant?.FontDescriptor;

    internal protected override SKTypeface Typeface => PrimaryDescendant?.Typeface;

    protected internal override CMapWMode WritingMode => _writingMode;

    protected internal override PdfSubstitutionInfo SubstitutionInfo => PrimaryDescendant?.SubstitutionInfo ?? PdfSubstitutionInfo.Detault;
    
    /// <summary>
    /// Descendant CID fonts that contain the actual font data.
    /// </summary>
    public List<PdfCidFont> DescendantFonts { get; }
    
    /// <summary>
    /// Primary descendant font (first in array, handles most characters)
    /// This is where most properties are inherited from
    /// </summary>
    public PdfCidFont PrimaryDescendant => DescendantFonts?.Count > 0 ? DescendantFonts[0] : null;

    /// <summary>
    /// Optional code->CID CMap derived from the parent /Encoding entry when it is a CMap stream.
    /// May be null if /Encoding is a predefined name without an embedded stream (e.g., Identity-H).
    /// </summary>
    public PdfCMap CodeToCidCMap { get; }

    /// <summary>
    /// CMap name from /Encoding entry (either predefined name or name from embedded CMap).
    /// </summary>
    public PdfString CMapName { get; }

    /// <summary>
    /// Check if font has embedded data (delegated to primary descendant)
    /// </summary>
    public override bool IsEmbedded => PrimaryDescendant?.IsEmbedded == true;

    /// <summary>
    /// Get character width (delegated to appropriate descendant CID font by CID).
    /// </summary>
    public override float GetWidth(PdfCharacterCode code)
    {
        var descendant = PrimaryDescendant;
        if (descendant == null)
        {
            return 0f;
        }

        uint cid;

        if (!TryMapCodeToCid(code, out cid))
        {
            return 0f;
        }

        return descendant.GetWidthByCid(cid);
    }

    public override VerticalMetric GetVerticalDisplacement(PdfCharacterCode code)
    {
        if (_writingMode == CMapWMode.Horizontal)
        {
            return default;
        }

        var descendant = PrimaryDescendant;
        if (descendant == null)
        {
            return default;
        }

        if (!TryMapCodeToCid(code, out uint cid))
        {
            return default;
        }

        return descendant.GetVerticalDisplacementByCid(cid);
    }

    /// <summary>
    /// Try to map a length-aware content code (PdfCid) to a numeric CID using the parent encoding.
    /// For Identity-H/V, the mapping is an identity of the big-endian integer value.
    /// For embedded CMap streams, uses the parsed CodeToCidCMap.
    /// </summary>
    public bool TryMapCodeToCid(PdfCharacterCode code, out uint cid)
    {
        var map = CodeToCidCMap;
        if (map != null && map.TryGetCid(code, out int mapped))
        {
            cid = (uint)mapped;
            return true;
        }

        cid = 0;
        return false;
    }

    /// <summary>
    /// Load descendant fonts (heavy operation - lazy loaded using GetPageObjects)
    /// </summary>
    private List<PdfCidFont> LoadDescendantFonts()
    {
        var descendants = new List<PdfCidFont>();

        // Use GetPageObjects to get all descendant font objects
        var descendantObjects = Dictionary.GetObjects(PdfTokens.DescendantFontsKey);
        if (descendantObjects == null || descendantObjects.Count == 0)
        {
            return descendants;
        }

        foreach (var descendantObj in descendantObjects)
        {
            var descendant = PdfFontFactory.CreateFont(descendantObj);

            if (descendant is PdfCidFont cidFont)
            {
                descendants.Add(cidFont);
            }

        }

        return descendants;
    }

    /// <summary>
    /// Load an embedded /Encoding CMap stream (if present) into a code->CID map.
    /// Returns null if /Encoding is a name or if parsing fails.
    /// </summary>
    private (PdfCMap CMap, PdfString CMapName) LoadCodeToCidCMap()
    {
        var predefinedName = Dictionary.GetName(PdfTokens.EncodingKey);

        if (!predefinedName.IsEmpty)
        {
            return (Document.CMapCache.GetCmap(predefinedName), predefinedName);
        }

        var encodingObj = Dictionary.GetObject(PdfTokens.EncodingKey);
        if (encodingObj == null)
        {
            return default;
        }

        if (encodingObj.Reference.IsValid && Document.CMapCache.CMapStreams.TryGetValue(encodingObj.Reference, out var cachedCMap))
        {
            return (cachedCMap, cachedCMap.Name);
        }

        var data = encodingObj.DecodeAsMemory();
        if (data.IsEmpty || data.Length == 0)
        {
            return default;
        }

        var result = PdfCMapParser.ParseCMap(data, Document);
        // PdfTokens.CMapNameKey
        
        var cmapNameToken = PdfString.FromString("CMapName"); // TODO: [HIGH] we need to cleanup the rest here, some other properties are coming from CMap
        var cmapName = encodingObj.Dictionary.GetName(cmapNameToken);

        if (!cmapName.IsEmpty)
        {
            result.Name = cmapName;
        };

        if (encodingObj.Reference.IsValid)
        {
            Document.CMapCache.CMapStreams[encodingObj.Reference] = result;
        }

        return (result, result.Name);
    }

    /// <summary>
    /// Extracts character codes from raw bytes for composite fonts.
    /// Uses codespace ranges if ToUnicodeCMap is available and valid; otherwise uses code length.
    /// </summary>
    /// <param name="bytes">Raw bytes to extract character codes from.</param>
    /// <returns>Array of extracted PdfCharacterCode items.</returns>
    public override PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return Array.Empty<PdfCharacterCode>();
        }

        if (CodeToCidCMap != null && CodeToCidCMap.HasCodeSpaceRanges)
        {
            var cmap = CodeToCidCMap;
            var characterCodes = new List<PdfCharacterCode>();
            int offset = 0;
            while (offset < bytes.Length)
            {
                int length = cmap.GetMaxMatchingLength(bytes.Slice(offset).Span);
                if (length == 0)
                {
                    length = 1;
                }
                characterCodes.Add(new PdfCharacterCode(bytes.Slice(offset, length)));
                offset += length;
            }
            return characterCodes.ToArray();
        }

        // fallback: fixed 2-byte codes
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
    /// Gets the glyph ID (GID) for the specified character code in a composite font.
    /// Returns 0 if no valid GID is found.
    /// Follows PDF spec: character code is mapped to CID using encoding/CMap, then CID is mapped to GID by descendant font.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public override ushort GetGid(PdfCharacterCode code)
    {
        if (code == null)
        {
            return 0;
        }

        var descendant = PrimaryDescendant;
        if (descendant == null)
        {
            return 0;
        }

        uint cid;
        if (!TryMapCodeToCid(code, out cid))
        {
            return 0;
        }

        // Call GetGlyphId of CID font directly
        return descendant.GetGidByCid(cid);
    }

    public override string GetUnicodeString(PdfCharacterCode code)
    {
        var baseCode = base.GetUnicodeString(code);

        if (baseCode != null)
        {
            return baseCode;
        }

        uint cid;

        if (!TryMapCodeToCid(code, out cid))
        {
            return null;
        }

        if (_toUnicode != null && _toUnicode.TryGetValue(cid, out var resultString))
        {
            return resultString;
        }

        return null;
    }
}