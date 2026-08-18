using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Type0 (Composite) fonts: Multi-byte character support
/// Acts as a wrapper that delegates to descendant CID fonts for actual rendering
/// Handles character encoding and script/language coordination.
/// </summary>
public class PdfCompositeFont : PdfFontBase
{
    /// <summary>
    /// The longest /UseCMap chain followed when loading an embedded CMap stream.
    /// </summary>
    private const int MaxUseCMapDepth = 8;

    private readonly CMapWMode _writingMode;
    private readonly Dictionary<uint, string>? _toUnicode;

    internal PdfCompositeFont(PdfObject fontObject)
        : base(fontObject)
    {
        DescendantFonts = LoadDescendantFonts();
        CodeToCidCMap = LoadCodeToCidCMap();
        _writingMode = CodeToCidCMap?.WMode ?? CMapWMode.Horizontal;
        _toUnicode = PdfToUnicodeMapProvider.GetToUnicodeMap(PrimaryDescendant?.CidSystemInfo);
    }

    /// <summary>
    /// The font descriptor inherited from the primary descendant CID font.
    /// </summary>
    public override PdfFontDescriptor? FontDescriptor => PrimaryDescendant?.FontDescriptor;

    /// <summary>
    /// The typeface provided by the primary descendant CID font, or <see langword="null"/> when unavailable.
    /// </summary>
    protected internal override IPdfTypeface? Typeface => PrimaryDescendant?.Typeface;

    /// <summary>
    /// The writing mode (horizontal or vertical) determined from the CMap's WMode entry.
    /// </summary>
    protected internal override CMapWMode WritingMode => _writingMode;

    /// <summary>
    /// The substitution info inherited from the primary descendant CID font, or the default value when no descendant is present.
    /// </summary>
    protected internal override PdfSubstitutionInfo SubstitutionInfo => PrimaryDescendant?.SubstitutionInfo ?? PdfSubstitutionInfo.Default;

    /// <summary>
    /// <see langword="true"/> when the primary descendant CID font has widths, <see langword="false"/> when there is no descendant.
    /// </summary>
    protected internal override bool HasWidths => PrimaryDescendant?.HasWidths == true;

    /// <summary>
    /// Descendant CID fonts that contain the actual font data.
    /// </summary>
    public List<PdfCidFont> DescendantFonts { get; }

    /// <summary>
    /// Primary descendant font (first in array, handles most characters)
    /// This is where most properties are inherited from
    /// </summary>
    public PdfCidFont? PrimaryDescendant => (DescendantFonts?.Count > 0) ? DescendantFonts[0] : null;

    /// <summary>
    /// Optional code->CID CMap derived from the parent /Encoding entry when it is a CMap stream.
    /// May be null if /Encoding is a predefined name without an embedded stream (e.g., Identity-H).
    /// </summary>
    public PdfCMap? CodeToCidCMap { get; }

    /// <summary>
    /// Get character width (delegated to appropriate descendant CID font by CID).
    /// </summary>
    public override float? GetWidth(PdfCharacterCode code)
    {
        PdfCidFont? descendant = PrimaryDescendant;
        if (descendant == null)
        {
            return null;
        }

        if (!TryMapCodeToCid(code, out uint cid))
        {
            return null;
        }

        return descendant.GetWidthByCid(cid);
    }

    /// <summary>
    /// Returns the vertical displacement for the specified character code when the font is in vertical writing mode.
    /// Returns the default metric when horizontal writing mode is active or no descendant font is present.
    /// </summary>
    /// <param name="code">The character code to retrieve vertical metrics for.</param>
    /// <returns>The <see cref="PdfVerticalMetric"/> for the character code.</returns>
    public override PdfVerticalMetric GetVerticalDisplacement(PdfCharacterCode code)
    {
        if (_writingMode == CMapWMode.Horizontal)
        {
            return default;
        }

        PdfCidFont? descendant = PrimaryDescendant;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryMapCodeToCid(PdfCharacterCode code, out uint cid)
    {
        PdfCMap? map = CodeToCidCMap;
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
        List<PdfCidFont> descendants = [];

        // Use GetPageObjects to get all descendant font objects
        List<PdfObject>? descendantObjects = Dictionary.GetObjects(PdfTokens.DescendantFontsKey);
        if (descendantObjects == null || descendantObjects.Count == 0)
        {
            return descendants;
        }

        foreach (PdfObject descendantObj in descendantObjects)
        {
            PdfFontBase? descendant = PdfFontFactory.CreateFont(descendantObj);

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
    private PdfCMap? LoadCodeToCidCMap()
    {
        PdfString? predefinedName = Dictionary.GetName(PdfTokens.EncodingKey);

        if (predefinedName != null)
        {
            return Document.CMapCache.GetCmap(predefinedName.Value);
        }

        PdfObject? encodingObject = Dictionary.GetObject(PdfTokens.EncodingKey);
        if (encodingObject == null)
        {
            return default;
        }

        return LoadCMapStream(encodingObject, 0);
    }

    /// <summary>
    /// Parses a CMap stream object into a <see cref="PdfCMap"/>, applying the CMap entries of its stream
    /// dictionary on top of the stream body, and caches the result under the object's reference.
    /// Returns <see langword="null"/> when the stream carries no data or does not parse.
    /// </summary>
    /// <param name="cmapObject">The CMap stream object to parse.</param>
    /// <param name="useCMapDepth">The number of /UseCMap entries followed to reach this stream.</param>
    private PdfCMap? LoadCMapStream(PdfObject cmapObject, int useCMapDepth)
    {
        if (cmapObject.Reference.IsValid && Document.CMapCache.CMapStreams.TryGetValue(cmapObject.Reference, out PdfCMap? cachedCMap))
        {
            return cachedCMap;
        }

        ReadOnlyMemory<byte> data = cmapObject.DecodeAsMemory();
        if (data.IsEmpty)
        {
            return null;
        }

        PdfCMap result = PdfCMapScanner.Scan(data, Document.CMapCache.GetCmap);

        ApplyCMapStreamDictionary(cmapObject.Dictionary, result, useCMapDepth);

        if (cmapObject.Reference.IsValid)
        {
            Document.CMapCache.CMapStreams[cmapObject.Reference] = result;
        }

        return result;
    }

    /// <summary>
    /// Applies the /CMapName, /CIDSystemInfo, /WMode and /UseCMap entries of a CMap stream dictionary to the
    /// CMap parsed from the stream body. /CMapName replaces the parsed name; /CIDSystemInfo and /WMode fill in
    /// what the body left at its default; /UseCMap contributes the mappings and codespace ranges of the CMap it
    /// designates, which the body's own entries take precedence over.
    /// </summary>
    /// <param name="cmapDictionary">The stream dictionary of the CMap being loaded.</param>
    /// <param name="cmap">The CMap parsed from the stream body.</param>
    /// <param name="useCMapDepth">The number of /UseCMap entries followed to reach this dictionary.</param>
    private void ApplyCMapStreamDictionary(PdfDictionary cmapDictionary, PdfCMap cmap, int useCMapDepth)
    {
        MergeUseCMap(cmapDictionary, cmap, useCMapDepth);

        PdfString? cmapName = cmapDictionary.GetName(PdfTokens.CMapNameKey);
        if (cmapName != null)
        {
            cmap.Name = cmapName;
        }

        if (cmap.CidSystemInfo == null)
        {
            cmap.CidSystemInfo = PdfCidSystemInfo.FromDictionary(cmapDictionary.GetDictionary(PdfTokens.CidSystemInfoKey));
        }

        if (cmap.WMode == CMapWMode.Horizontal)
        {
            int? writingMode = cmapDictionary.GetInteger(PdfTokens.WModeKey);
            if (writingMode != null)
            {
                cmap.WMode = (CMapWMode)writingMode.Value;
            }
        }
    }

    /// <summary>
    /// Merges the CMap designated by the /UseCMap entry of a CMap stream dictionary into <paramref name="cmap"/>.
    /// The entry is either the name of a predefined CMap or a stream holding a further CMap, and chains longer
    /// than <see cref="MaxUseCMapDepth"/> entries are not followed.
    /// </summary>
    /// <param name="cmapDictionary">The stream dictionary of the CMap being loaded.</param>
    /// <param name="cmap">The CMap to merge the designated CMap into.</param>
    /// <param name="useCMapDepth">The number of /UseCMap entries followed to reach this dictionary.</param>
    private void MergeUseCMap(PdfDictionary cmapDictionary, PdfCMap cmap, int useCMapDepth)
    {
        if (useCMapDepth >= MaxUseCMapDepth)
        {
            return;
        }

        PdfString? useCMapName = cmapDictionary.GetName(PdfTokens.UseCMapKey);
        if (useCMapName != null)
        {
            PdfCMap? predefinedCMap = Document.CMapCache.GetCmap(useCMapName.Value);
            if (predefinedCMap != null)
            {
                cmap.MergeFrom(predefinedCMap);
            }

            return;
        }

        PdfObject? useCMapObject = cmapDictionary.GetObject(PdfTokens.UseCMapKey);
        if (useCMapObject == null)
        {
            return;
        }

        PdfCMap? baseCMap = LoadCMapStream(useCMapObject, useCMapDepth + 1);
        if (baseCMap != null)
        {
            cmap.MergeFrom(baseCMap);
        }
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

        if (CodeToCidCMap?.HasCodeSpaceRanges == true)
        {
            PdfCMap cmap = CodeToCidCMap;
            List<PdfCharacterCode> characterCodes = [];
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
    /// Follows PDF spec: character code is mapped to CID using encoding/CMap, then CID is mapped to GID by descendant font.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID (GID) for the character code, or <see langword="null"/> if not found.</returns>
    public override ushort? GetGid(PdfCharacterCode code)
    {
        if (code == null)
        {
            return null;
        }

        PdfCidFont? descendant = PrimaryDescendant;
        if (descendant == null)
        {
            return null;
        }

        uint cid;
        if (!TryMapCodeToCid(code, out cid))
        {
            return null;
        }

        return descendant.GetGidByCid(cid);
    }

    /// <summary>
    /// Resolves glyphs for a composite code through the descendant font's CID mapping. A CID addresses
    /// a glyph in the embedded program alone and means nothing to an installed font, so once that
    /// program is unavailable the Unicode the code stands for is the only channel left.
    /// </summary>
    protected override PdfGlyphResolution ResolveGlyphs(PdfCharacterCode characterCode, string? renderingUnicode)
    {
        PdfGlyphResolution resolution = ResolveFromFontProgram(characterCode, renderingUnicode);

        return (!resolution.IsEmpty) ? resolution : SubstituteByUnicode(renderingUnicode);
    }

    /// <summary>
    /// Converts a character code to its Unicode string representation.
    /// First consults the ToUnicode CMap from the base class; if that yields no result, maps the code to a CID and
    /// looks it up in the built-in CID-to-Unicode table for the font's CIDSystemInfo; if that also fails, reads the
    /// CID values of a /ToUnicode CMap that declares no <c>bfchar</c>/<c>bfrange</c> entries as Unicode code points;
    /// if that also fails, falls back to treating the character code itself as a Unicode code point.
    /// </summary>
    /// <param name="code">The character code to convert.</param>
    /// <returns>The Unicode string for the character code, or <see langword="null"/> if no mapping is found.</returns>
    public override string? GetUnicodeString(PdfCharacterCode code)
    {
        string? baseCode = base.GetUnicodeString(code);

        if (baseCode != null)
        {
            return baseCode;
        }

        if (TryMapCodeToCid(code, out uint cid) && _toUnicode != null && _toUnicode.TryGetValue(cid, out string? resultString))
        {
            return resultString;
        }

        if (TryGetToUnicodeCidAsCodePoint(code, out string? cidCodePoint))
        {
            return cidCodePoint;
        }

        // Fallback: treat the character code itself as a Unicode code point.
        var codePoint = (int)(uint)code;
        return (PdfCMap.IsValidCodePoint(codePoint)) ? char.ConvertFromUtf32(codePoint) : null;
    }

    /// <summary>
    /// Converts a character code to the Unicode string used to select and shape a substitute glyph.
    /// When the descendant is a non-embedded CIDFontType2 font with an Identity CID ordering and the
    /// /ToUnicode CMap has no genuine bfchar/bfrange entries (only cidchar/cidrange, which some
    /// producers mislabel as /ToUnicode), resolves the glyph index against the built-in standard-font
    /// glyph map instead, since such fonts commonly address a well-known non-embedded font by glyph
    /// index. A /CIDToGIDMap turns the CID into that glyph index; without one the CID is the glyph
    /// index itself. Falls back to <see cref="GetUnicodeString"/> in every other case.
    /// </summary>
    /// <param name="code">The character code to convert.</param>
    /// <returns>The Unicode string to shape against, or <see langword="null"/> if none is available.</returns>
    public override string? GetRenderingUnicodeString(PdfCharacterCode code)
    {
        if (ToUnicodeCMap?.HasUnicodeMappings != true
            && TryMapCodeToCid(code, out uint cid)
            && TryGetStandardFontGlyphUnicode(cid, out string? standardFontUnicode))
        {
            return standardFontUnicode;
        }

        return base.GetRenderingUnicodeString(code);
    }

    private bool TryGetStandardFontGlyphUnicode(uint cid, out string? unicode)
    {
        PdfCidFont? descendant = PrimaryDescendant;
        if (descendant == null
            || descendant.Type != PdfFontSubType.CidFontType2
            || descendant.Typeface != null
            || descendant.CidSystemInfo == null
            || descendant.CidSystemInfo.Ordering != PdfTokens.IdentityKey)
        {
            unicode = null;
            return false;
        }

        PdfCidToGidMap? cidToGidMap = descendant.CidToGidMap;
        uint glyphIndex = cid;

        if (cidToGidMap != null)
        {
            ushort? cidToGidIndex = cidToGidMap.GetGID(cid);

            if (cidToGidIndex == null)
            {
                unicode = null;
                return false;
            }

            glyphIndex = cidToGidIndex.Value;
        }

        return StandardFontGlyphMapProvider.TryGetUnicode(descendant.BaseFont, glyphIndex, out unicode);
    }

    // A /ToUnicode CMap that declares only cidchar/cidrange entries carries its target values as CIDs
    // rather than as the byte strings bfchar/bfrange would use; those values are Unicode code points.
    private bool TryGetToUnicodeCidAsCodePoint(PdfCharacterCode code, out string? unicode)
    {
        PdfCMap? toUnicodeCMap = ToUnicodeCMap;

        if (toUnicodeCMap == null
            || toUnicodeCMap.HasUnicodeMappings
            || !toUnicodeCMap.TryGetCid(code, out int cid)
            || !PdfCMap.IsValidCodePoint(cid))
        {
            unicode = null;
            return false;
        }

        unicode = char.ConvertFromUtf32(cid);
        return true;
    }
}
