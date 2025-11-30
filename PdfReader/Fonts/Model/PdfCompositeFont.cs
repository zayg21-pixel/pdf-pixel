using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Type0 (Composite) fonts: Multi-byte character support
/// Acts as a wrapper that delegates to descendant CID fonts for actual rendering
/// Handles character encoding and script/language coordination.
/// </summary>
public class PdfCompositeFont : PdfFontBase
{
    Encoding _cmapEncoding;
    private CMapWMode _writingMode;

    static PdfCompositeFont()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Constructor for composite fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    public PdfCompositeFont(PdfDictionary fontDictionary) : base(fontDictionary)
    {
        DescendantFonts = LoadDescendantFonts();
        (CodeToCidCMap, CMapName) = LoadCodeToCidCMap();
        _writingMode = CodeToCidCMap?.WMode ?? CMapWMode.Horizontal;

        // TODO: cleanup
        /*
Japanese	*-RKSJ-*	932
Japanese	EUC-*	EUC-JP
Simplified Chinese	GB-EUC-*	gb2312
Simplified Chinese	GBK-*	936
Traditional Chinese	B5-*, ETen-*	950
Korean	KSCms-UHC-*	949
Korean	KSCpc-EUC-*	euc-kr
Korean	KSC-Johab-*	1361
         */

        if (!CMapName.IsEmpty)
        {
            // TODO: this is incorrect, we need cid2code maps for proper handling. We don't even need those complex heuristics here,
            // what we should do is to map to UTF-16 column directly using cid2code maps
            var splitTokens = new HashSet<string>(CMapName.ToString().Split('-'));

            if (splitTokens.Contains("RKSJ"))
            {
                _cmapEncoding = Encoding.GetEncoding("shift_jis");
            }
            else if (splitTokens.Contains("GB") && splitTokens.Contains("EUC"))
            {
                _cmapEncoding = Encoding.GetEncoding("EUC-JP");
            }
            else if (splitTokens.Contains("EUC"))
            {
                _cmapEncoding = Encoding.GetEncoding("gb2312");
            }
            else if (splitTokens.Contains("UTF8"))
            {
                _cmapEncoding = Encoding.UTF8;
            }
            else if (splitTokens.Contains("UTF16"))
            {
                _cmapEncoding = Encoding.BigEndianUnicode;
            }
            else if (splitTokens.Contains("UCS2"))
            {
                _cmapEncoding = Encoding.BigEndianUnicode;
            }
            else if (splitTokens.Contains("Identity"))
            {
                if (PrimaryDescendant.CidSystemInfo.Ordering.ToString().Contains("Japan"))
                {
                    _cmapEncoding = Encoding.GetEncoding("gb2312");
                }
                else
                {
                    _cmapEncoding = Encoding.BigEndianUnicode;
                }
            }
        }
    }

    public override PdfFontDescriptor FontDescriptor => PrimaryDescendant?.FontDescriptor;

    internal protected override SKTypeface Typeface => PrimaryDescendant?.Typeface;

    protected internal override CMapWMode WritingMode => _writingMode;
    
    /// <summary>
    /// Descendant CID fonts that contain the actual font data.
    /// </summary>
    public List<PdfCidFont> DescendantFonts { get; }
    
    /// <summary>
    /// Primary descendant font (first in array, handles most characters)
    /// This is where most properties are inherited from
    /// </summary>
    public PdfCidFont PrimaryDescendant => DescendantFonts.Count > 0 ? DescendantFonts[0] : null;

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

        uint cid;

        if (!TryMapCodeToCid(code, out cid))
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
        // TODO: optimize for Identity-H/V
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
            var descendant = PdfFontFactory.CreateFont(descendantObj.Dictionary);

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
            return (Document.GetCmap(predefinedName), predefinedName);
        }

        var encodingObj = Dictionary.GetObject(PdfTokens.EncodingKey);
        if (encodingObj == null)
        {
            return default;
        }

        if (encodingObj.Reference.IsValid && Document.CMapStreamCache.TryGetValue(encodingObj.Reference, out var cachedCMap))
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
        var cmapNameToken = PdfString.FromString("CMapName"); // TODO: we need to cleanup the rest here, some other properties are coming from CMap
        var cmapName = encodingObj.Dictionary.GetName(cmapNameToken);

        if (!cmapName.IsEmpty)
        {
            result.Name = cmapName;
        };

        if (encodingObj.Reference.IsValid)
        {
            Document.CMapStreamCache[encodingObj.Reference] = result;
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

        // TODO: this seems to work fine, but font substitution should use encoding as hint
        if (_cmapEncoding != null)
        {
            var result = _cmapEncoding.GetString(code.Bytes);
            return result;
        }

        return null;
    }
}