using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Orchestrates parsing of a CFF typeface: uses <see cref="CffBinaryReader"/> to read binary blocks
/// (header, INDEX structures) and delegates slices of the data to the relevant block parsers.
/// </summary>
public class CffTypefaceReader
{
    private readonly ILogger<CffTypefaceReader> _logger;
    private readonly CffTopDictReader _topDictReader;
    private readonly CffPrivateDictReader _privateDictReader;
    private readonly CffFdSelectReader _fdSelectReader;
    private readonly CffCharsetReader _charsetReader;
    private readonly CffEncodingReader _encodingReader;
    private readonly CffCharStringEvaluator _charStringEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffTypefaceReader"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public CffTypefaceReader(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CffTypefaceReader>();
        _topDictReader = new CffTopDictReader(loggerFactory.CreateLogger<CffTopDictReader>());
        _privateDictReader = new CffPrivateDictReader(loggerFactory.CreateLogger<CffPrivateDictReader>());
        _fdSelectReader = new CffFdSelectReader(loggerFactory.CreateLogger<CffFdSelectReader>());
        _charsetReader = new CffCharsetReader(loggerFactory.CreateLogger<CffCharsetReader>());
        _encodingReader = new CffEncodingReader(loggerFactory.CreateLogger<CffEncodingReader>());
        _charStringEvaluator = new CffCharStringEvaluator(loggerFactory.CreateLogger<CffCharStringEvaluator>());
    }

    /// <summary>
    /// Reads a CFF typeface from its raw table bytes.
    /// </summary>
    /// <param name="cffData">The raw CFF table bytes.</param>
    /// <returns>The parsed typeface, or null if the data is structurally malformed.</returns>
    public CffTypeface? Read(in ReadOnlyMemory<byte> cffData)
    {
        CffBinaryReader headerReader = new(cffData.Span);

        if (!headerReader.TryReadByte(out byte majorVersion))
        {
            _logger.LogWarning("Failed to read CFF header: missing major version byte.");
            return null;
        }

        if (!headerReader.TryReadByte(out byte minorVersion))
        {
            _logger.LogWarning("Failed to read CFF header: missing minor version byte.");
            return null;
        }

        if (!headerReader.TryReadByte(out byte headerSize))
        {
            _logger.LogWarning("Failed to read CFF header: missing header size byte.");
            return null;
        }

        if (!headerReader.TryReadByte(out _))
        {
            _logger.LogWarning("Failed to read CFF header: missing offSize byte.");
            return null;
        }

        ReadOnlyMemory<byte> body = cffData.Slice(headerSize);
        CffBinaryReader reader = new(body.Span);

        if (!TryReadEntries(ref reader, body, out ReadOnlyMemory<byte>[] names))
        {
            _logger.LogWarning("Failed to read CFF Name INDEX.");
            return null;
        }

        if (!TryReadEntries(ref reader, body, out ReadOnlyMemory<byte>[] topDicts))
        {
            _logger.LogWarning("Failed to read CFF Top DICT INDEX.");
            return null;
        }

        if (!TryReadEntries(ref reader, body, out ReadOnlyMemory<byte>[] strings))
        {
            _logger.LogWarning("Failed to read CFF String INDEX.");
            return null;
        }

        if (!TryReadEntries(ref reader, body, out ReadOnlyMemory<byte>[] globalSubrs))
        {
            _logger.LogWarning("Failed to read CFF Global Subr INDEX.");
            return null;
        }

        if (names.Length != topDicts.Length)
        {
            _logger.LogWarning("CFF Name INDEX has {NameCount} entries but Top DICT INDEX has {TopDictCount}.", names.Length, topDicts.Length);
            return null;
        }

        var fonts = new CffFont[names.Length];
        for (int fontIndex = 0; fontIndex < fonts.Length; fontIndex++)
        {
            fonts[fontIndex] = ReadFont(names[fontIndex], topDicts[fontIndex], cffData, globalSubrs, strings);
        }

        return new CffTypeface
        {
            MajorVersion = majorVersion,
            MinorVersion = minorVersion,
            Fonts = fonts,
            Strings = strings,
            GlobalSubrs = globalSubrs
        };
    }

    private CffFont ReadFont(
        in ReadOnlyMemory<byte> nameEntry,
        in ReadOnlyMemory<byte> topDictEntry,
        in ReadOnlyMemory<byte> cffData,
        ReadOnlyMemory<byte>[] globalSubrs,
        ReadOnlyMemory<byte>[] strings)
    {
        CffTopDict topDict = _topDictReader.Read(topDictEntry.Span);
        CffPrivateDict? privateDict = ReadPrivateDict(topDict, cffData);
        CffFontDict dict = new() { TopDict = topDict, PrivateDict = privateDict };
        ReadOnlyMemory<byte>[] charStrings = ReadCharStrings(topDict, cffData);
        CffFontDict[] fdArray = ReadFdArray(topDict, cffData);
        CffFdSelect? fdSelect = ReadFdSelect(topDict, charStrings.Length, cffData);

        CffCharset? charset = _charsetReader.Read(cffData.Span, topDict.CharsetOffset ?? 0, charStrings.Length);

        var characters = new CffCharacter[charStrings.Length];
        Dictionary<PdfFontString, ushort>? fontNameToGid = null;

        if (fdArray.Length > 0 && fdSelect != null)
        {
            // CID-keyed fonts carry CIDs rather than glyph-name SIDs in their charset, and the deprecated
            // seac-style endchar accent composition is not used with CIDFonts, so no name resolution is
            // needed here.
            Dictionary<PdfFontString, ushort> nameToGid = [];

            for (int glyphIndex = 0; glyphIndex < charStrings.Length; glyphIndex++)
            {
                CffFontDict? glyphDict = null;
                ReadOnlyMemory<byte>[] glyphLocalSubrs;

                int fdIndex = (glyphIndex < fdSelect.FdIndexByGid.Length) ? fdSelect.FdIndexByGid[glyphIndex] : -1;
                if (fdIndex >= 0 && fdIndex < fdArray.Length)
                {
                    glyphDict = fdArray[fdIndex];
                    glyphLocalSubrs = ReadLocalSubrs(glyphDict.TopDict, glyphDict.PrivateDict, cffData);
                }
                else
                {
                    if (fdIndex >= 0)
                    {
                        _logger.LogWarning("CFF FDSelect references out-of-range Font DICT {FdIndex} (of {Count}) for GID {Gid}.", fdIndex, fdArray.Length, glyphIndex);
                    }

                    glyphLocalSubrs = ReadLocalSubrs(topDict, privateDict, cffData);
                }

                characters[glyphIndex] = _charStringEvaluator.Evaluate(charStrings[glyphIndex], dict, glyphDict, glyphLocalSubrs, globalSubrs, charStrings, nameToGid);
            }
        }
        else
        {
            Dictionary<PdfFontString, ushort> nameToGid = BuildNameToGid(charset, strings);
            fontNameToGid = nameToGid;
            ReadOnlyMemory<byte>[] localSubrs = ReadLocalSubrs(topDict, privateDict, cffData);
            for (int glyphIndex = 0; glyphIndex < charStrings.Length; glyphIndex++)
            {
                characters[glyphIndex] = _charStringEvaluator.Evaluate(charStrings[glyphIndex], dict, null, localSubrs, globalSubrs, charStrings, nameToGid);
            }
        }

        CffEncoding? encoding = ReadEncoding(topDict, fdArray, charset, cffData);

        return new CffFont
        {
            Name = nameEntry,
            Dict = dict,
            FdArray = fdArray,
            FdSelect = fdSelect,
            Charset = charset,
            Encoding = encoding,
            CodeToName = BuildCodeToName(encoding, charset, strings),
            NameToGid = fontNameToGid,
            Characters = characters
        };
    }

    private static PdfFontString[]? BuildCodeToName(CffEncoding? encoding, CffCharset? charset, ReadOnlyMemory<byte>[] strings)
    {
        if (encoding == null || charset == null)
        {
            return null;
        }

        var codeToName = new PdfFontString[256];
        for (int code = 0; code < codeToName.Length; code++)
        {
            ushort gid = encoding.GidByCode[code];
            if (gid >= charset.SidsByGid.Length)
            {
                continue;
            }

            codeToName[code] = CffCharset.ResolveGlyphName(charset.SidsByGid[gid], strings);
        }

        return codeToName;
    }

    private CffEncoding? ReadEncoding(CffTopDict topDict, CffFontDict[] fdArray, CffCharset? charset, in ReadOnlyMemory<byte> cffData)
    {
        // CID-keyed fonts select glyphs by CID via the charset, not by character code -- the CFF spec
        // requires Encoding to be absent (or predefined/unused) for them, so there is nothing to read.
        if (fdArray.Length > 0 || charset == null)
        {
            return null;
        }

        int encodingOffset = topDict.EncodingOffset ?? CffConstants.EncodingPredefinedStandard;
        return _encodingReader.Read(cffData.Span, encodingOffset, charset);
    }

    private static Dictionary<PdfFontString, ushort> BuildNameToGid(CffCharset? charset, ReadOnlyMemory<byte>[] strings)
    {
        Dictionary<PdfFontString, ushort> nameToGid = [];
        if (charset == null)
        {
            return nameToGid;
        }

        ushort[] sidsByGid = charset.SidsByGid;
        for (ushort glyphId = 0; glyphId < sidsByGid.Length; glyphId++)
        {
            PdfFontString glyphName = CffCharset.ResolveGlyphName(sidsByGid[glyphId], strings);
            if (!glyphName.IsEmpty && !nameToGid.ContainsKey(glyphName))
            {
                nameToGid[glyphName] = glyphId;
            }
        }

        return nameToGid;
    }

    private CffPrivateDict? ReadPrivateDict(CffTopDict topDict, in ReadOnlyMemory<byte> cffData)
    {
        if (topDict.PrivateDictOffset == null || topDict.PrivateDictSize == null)
        {
            return null;
        }

        int start = topDict.PrivateDictOffset.Value;
        int size = topDict.PrivateDictSize.Value;
        if (start < 0 || size < 0 || start + size > cffData.Length)
        {
            _logger.LogWarning("Invalid Private DICT range: offset={Offset}, size={Size}, length={Length}.", start, size, cffData.Length);
            return null;
        }

        return _privateDictReader.Read(cffData.Span.Slice(start, size));
    }

    private CffFontDict[] ReadFdArray(CffTopDict topDict, in ReadOnlyMemory<byte> cffData)
    {
        if (topDict.FdArrayOffset == null)
        {
            return Array.Empty<CffFontDict>();
        }

        if (!TryReadEntriesAt(cffData, topDict.FdArrayOffset.Value, out ReadOnlyMemory<byte>[] fdEntries))
        {
            _logger.LogWarning("Failed to read CFF FDArray INDEX at offset {Offset}.", topDict.FdArrayOffset.Value);
            return Array.Empty<CffFontDict>();
        }

        var fdArray = new CffFontDict[fdEntries.Length];
        for (int fdIndex = 0; fdIndex < fdEntries.Length; fdIndex++)
        {
            CffTopDict fontDict = _topDictReader.Read(fdEntries[fdIndex].Span);
            fdArray[fdIndex] = new CffFontDict
            {
                TopDict = fontDict,
                PrivateDict = ReadPrivateDict(fontDict, cffData)
            };
        }

        return fdArray;
    }

    private CffFdSelect? ReadFdSelect(CffTopDict topDict, int glyphCount, in ReadOnlyMemory<byte> cffData)
    {
        if (topDict.FdSelectOffset == null)
        {
            return null;
        }

        int position = topDict.FdSelectOffset.Value;
        if (position < 0 || position >= cffData.Length)
        {
            _logger.LogWarning("Invalid CFF FDSelect offset {Offset}.", position);
            return null;
        }

        return _fdSelectReader.Read(cffData.Span.Slice(position), glyphCount);
    }

    private ReadOnlyMemory<byte>[] ReadCharStrings(CffTopDict topDict, in ReadOnlyMemory<byte> cffData)
    {
        if (topDict.CharStringsOffset == null)
        {
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        if (!TryReadEntriesAt(cffData, topDict.CharStringsOffset.Value, out ReadOnlyMemory<byte>[] charStrings))
        {
            _logger.LogWarning("Failed to read CFF CharStrings INDEX at offset {Offset}.", topDict.CharStringsOffset.Value);
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        return charStrings;
    }

    private ReadOnlyMemory<byte>[] ReadLocalSubrs(CffTopDict topDict, CffPrivateDict? privateDict, in ReadOnlyMemory<byte> cffData)
    {
        if (privateDict?.SubrsOffset == null || topDict.PrivateDictOffset == null)
        {
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        int position = topDict.PrivateDictOffset.Value + privateDict.SubrsOffset.Value;
        if (!TryReadEntriesAt(cffData, position, out ReadOnlyMemory<byte>[] localSubrs))
        {
            _logger.LogWarning("Failed to read CFF Local Subr INDEX at offset {Offset}.", position);
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        return localSubrs;
    }

    private static bool TryReadEntriesAt(in ReadOnlyMemory<byte> data, int position, out ReadOnlyMemory<byte>[] entries)
    {
        entries = Array.Empty<ReadOnlyMemory<byte>>();
        if (position < 0 || position >= data.Length)
        {
            return false;
        }

        ReadOnlyMemory<byte> slice = data.Slice(position);
        CffBinaryReader reader = new(slice.Span);
        return TryReadEntries(ref reader, slice, out entries);
    }

    private static bool TryReadEntries(ref CffBinaryReader reader, in ReadOnlyMemory<byte> data, out ReadOnlyMemory<byte>[] entries)
    {
        entries = Array.Empty<ReadOnlyMemory<byte>>();

        CffIndex? index = reader.ReadIndex();
        if (index == null)
        {
            return false;
        }

        CffIndex indexValue = index.Value;
        if (indexValue.Count == 0)
        {
            return true;
        }

        var result = new ReadOnlyMemory<byte>[indexValue.Count];
        for (int entryIndex = 0; entryIndex < indexValue.Count; entryIndex++)
        {
            int start = indexValue.DataStart + (indexValue.Offsets[entryIndex] - 1);
            int end = indexValue.DataStart + (indexValue.Offsets[entryIndex + 1] - 1);
            if (start < 0 || end < start || end > data.Length)
            {
                return false;
            }

            result[entryIndex] = data.Slice(start, end - start);
        }

        entries = result;
        return true;
    }
}
