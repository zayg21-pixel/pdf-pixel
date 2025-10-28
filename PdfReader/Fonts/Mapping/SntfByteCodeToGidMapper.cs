using PdfReader.Fonts.Types;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Provides mapping from PDF character codes to glyph IDs (GIDs) for SFNT-based fonts (TrueType/OpenType) using SKTypeface.
    /// For single-byte fonts, uses direct code-to-GID mapping (Format 0 CMap) as the primary method.
    /// Name-to-GID and Unicode-to-GID are used only as fallbacks if direct mapping is unavailable.
    /// </summary>
    internal class SntfByteCodeToGidMapper : IByteCodeToGidMapper
    {
        private readonly ushort[] _codeToGid; // Direct Format 0/4 for symbolic fonts
        private readonly Dictionary<string, ushort> _nameToGid;
        private readonly Dictionary<string, ushort> _unicodeToGid;
        private readonly PdfFontFlags _flags;
        private readonly PdfFontEncoding _encoding;
        private readonly Dictionary<int, string> _differences;
        private readonly PdfToUnicodeCMap _toUnicodeCMap;
        private readonly FontTableInfo _tableInfo;

        /// <summary>
        /// Initializes a new instance of <see cref="SntfByteCodeToGidMapper"/> for the specified typeface and encoding.
        /// </summary>
        /// <param name="typeface">The SKTypeface representing the font.</param>
        /// <param name="flags">Flags defined in PDF font.</param>
        /// <param name="encoding">The PDF font encoding.</param>
        /// <param name="differences">Encoding differences.</param>
        /// <param name="toUnicodeCMap">ToUnicode CMap for character mapping.</param>
        public SntfByteCodeToGidMapper(
            SKTypeface typeface,
            PdfFontFlags flags,
            PdfFontEncoding encoding,
            Dictionary<int, string> differences,
            PdfToUnicodeCMap toUnicodeCMap)
        {
            if (typeface == null)
            {
                throw new ArgumentNullException(nameof(typeface));
            }

            _flags = flags;
            _encoding = encoding;
            _differences = differences;
            _toUnicodeCMap = toUnicodeCMap;

            _tableInfo = GetFontTableInfo(typeface);
            _codeToGid = ExtractCodeToGidFormat0(_tableInfo); // Direct Format 0 mapping

            if (_codeToGid == null)
            {
                _codeToGid = ExtractCodeToGidFormat4(_tableInfo);
            }

            _nameToGid = ExtractNameToGid(_tableInfo);
            _unicodeToGid = ExtractUnicodeToGid(_tableInfo);
        }

        /// <summary>
        /// Gets the glyph ID (GID) for the specified character code.
        /// Uses identity mapping if specified; otherwise, resolves using encoding, name, or Unicode maps.
        /// Returns 0 if the mapping is not found.
        /// </summary>
        /// <param name="code">The PDF character code.</param>
        /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
        public ushort GetGid(byte code)
        {
            if (_flags.HasFlag(PdfFontFlags.Symbolic) && _codeToGid != null)
            {
                return _codeToGid[code];
            }

            string name = SingleByteEncodings.GetNameByCode(code, _encoding, _differences);

            if (name == null)
            {
                return 0;
            }

            if (_nameToGid.TryGetValue(name, out var gidByName))
            {
                return gidByName;
            }

            string unicode = _toUnicodeCMap?.GetUnicode(code);

            if (unicode == null)
            {
                AdobeGlyphList.CharacterMap.TryGetValue(name, out unicode);
            }

            if (unicode != null && _unicodeToGid.TryGetValue(unicode, out var gidByUnicode))
            {
                return gidByUnicode;
            }

            return 0;
        }

        /// <summary>
        /// Extracts font table data and offsets needed for mapping.
        /// </summary>
        /// <param name="typeface">The SKTypeface to inspect.</param>
        /// <returns>FontTableInfo struct with table data and offsets.</returns>
        private static FontTableInfo GetFontTableInfo(SKTypeface typeface)
        {
            FontTableInfo info = new FontTableInfo();

            uint postTag = ExtractHelpers.ConvertTagToUInt32("post");
            if (typeface.TryGetTableData(postTag, out byte[] postData) && postData != null && postData.Length >= 32)
            {
                info.PostData = postData;
                uint formatFixed = ExtractHelpers.ReadUInt32(postData, 0);
                info.PostDataFormat = formatFixed / 65536.0f;
            }

            uint cmapTag = ExtractHelpers.ConvertTagToUInt32("cmap");
            if (typeface.TryGetTableData(cmapTag, out byte[] cmapData) && cmapData != null && cmapData.Length >= 4)
            {
                info.CmapData = cmapData;
                ushort numTables = ExtractHelpers.ReadUInt16(cmapData, 2);
                info.Format0Offset = -1;
                info.Format4Offset = -1;
                info.Format0Encoding = PdfFontEncoding.Unknown;

                for (int tableIndex = 0; tableIndex < numTables; tableIndex++)
                {
                    int recordOffset = 4 + tableIndex * 8;
                    if (recordOffset + 8 > cmapData.Length)
                    {
                        continue;
                    }
                    uint subtableOffset = ExtractHelpers.ReadUInt32(cmapData, recordOffset + 4);
                    if (subtableOffset + 2 > cmapData.Length)
                    {
                        continue;
                    }
                    ushort format = ExtractHelpers.ReadUInt16(cmapData, (int)subtableOffset);
                    if (format == 0 && info.Format0Offset < 0)
                    {
                        info.Format0Offset = (int)subtableOffset;
                        info.Format0Encoding = SnftCMapParser.GetFormat0Encoding(cmapData, recordOffset);
                    }
                    else if (format == 4 && info.Format4Offset < 0)
                    {
                        info.Format4Offset = (int)subtableOffset;
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Extracts a direct mapping from byte code to GID using Format 0 CMap.
        /// Used as the primary mapping for single-byte fonts.
        /// </summary>
        private static ushort[] ExtractCodeToGidFormat0(FontTableInfo info)
        {
            if (info.CmapData != null && info.Format0Offset >= 0)
            {
                return SnftCMapParser.ParseFormat0(info.CmapData, info.Format0Offset);
            }
            return null;
        }

        /// <summary>
        /// Extracts a direct mapping from byte code to GID using Format 0 CMap.
        /// Used as the primary mapping for single-byte fonts.
        /// </summary>
        private static ushort[] ExtractCodeToGidFormat4(FontTableInfo info)
        {
            if (info.CmapData != null && info.Format4Offset >= 0)
            {
                var subResult = SnftCMapParser.ParseFormat4(info.CmapData, info.Format4Offset);
                ushort[] result = new ushort[256]; // this can be done better, I think

                foreach (var item in subResult)
                {
                    result[(byte)item.Key] = item.Value;
                }

                return result;
            }
            return null;
        }

        /// <summary>
        /// Extracts a mapping from glyph names to glyph IDs (GIDs) using the font's 'post' table and CMap format 0.
        /// Used only as a fallback if direct code-to-GID mapping is unavailable.
        /// </summary>
        /// <param name="info">FontTableInfo struct with table data and offsets.</param>
        /// <returns>Dictionary mapping glyph names to GIDs.</returns>
        private static Dictionary<string, ushort> ExtractNameToGid(FontTableInfo info)
        {
            var nameToGid = new Dictionary<string, ushort>(StringComparer.Ordinal);

            // Merge post table (format 1.0 or 2.0)
            if (info.PostData != null)
            {
                if (info.PostDataFormat == 1.0f)
                {
                    var postMap = SfntPostTableParser.GetNameToGidFormat1(info.PostData);
                    foreach (var kvp in postMap)
                    {
                        nameToGid[kvp.Key] = kvp.Value;
                    }
                }
                else if (info.PostDataFormat == 2.0f)
                {
                    var postMap = SfntPostTableParser.GetNameToGidFormat2(info.PostData);
                    foreach (var kvp in postMap)
                    {
                        nameToGid[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Merge CMap format 0
            if (info.CmapData != null && info.Format0Offset >= 0)
            {
                string[] encodingNames = SingleByteEncodings.GetEncodingSet(info.Format0Encoding);
                if (encodingNames != null)
                {
                    var cmapMap = SnftCMapParser.ParseFormat0(info.CmapData, info.Format0Offset);
                    for (int code = 0; code < 256; code++)
                    {
                        string glyphName = encodingNames[code];
                        
                        if (!string.IsNullOrEmpty(glyphName))
                        {
                            nameToGid[glyphName] = cmapMap[code];
                        }
                    }
                }
            }

            return nameToGid;
        }

        /// <summary>
        /// Extracts a mapping from Unicode codepoints to glyph IDs (GIDs) using CMap format 4.
        /// TODO: Add support for format 12 for large Unicode fonts.
        /// </summary>
        /// <param name="info">FontTableInfo struct with table data and offsets.</param>
        /// <returns>Dictionary mapping Unicode codepoints to GIDs.</returns>
        private static Dictionary<string, ushort> ExtractUnicodeToGid(FontTableInfo info)
        {
            var unicodeToGid = new Dictionary<string, ushort>();

            if (info.CmapData != null && info.Format4Offset >= 0)
            {
                var format4Map = SnftCMapParser.ParseFormat4(info.CmapData, info.Format4Offset);
                foreach (var kvp in format4Map)
                {
                    string unicodeString = char.ConvertFromUtf32(kvp.Key);
                    unicodeToGid[unicodeString] = kvp.Value;
                }
            }

            // TODO: Add support for format 12 (for large Unicode fonts)

            return unicodeToGid;
        }

        /// <summary>
        /// Holds extracted font table data and offsets for parsing.
        /// </summary>
        private struct FontTableInfo
        {
            public byte[] PostData;
            public float PostDataFormat;
            public byte[] CmapData;
            public int Format0Offset;
            public PdfFontEncoding Format0Encoding;
            public int Format4Offset;
        }
    }
}