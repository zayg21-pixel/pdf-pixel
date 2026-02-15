using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Text;
using PdfPixel.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Minimal CFF (Type 1C) reader utilities to get mappings for name-keyed CFF.
/// Converted to an instance class to allow structured logging via PdfDocument logger factory.
/// </summary>
internal class CffSidGidMapper
{
    private const int PredefinedEncodingStandard = 0;     // StandardEncoding id
    private const int PredefinedEncodingExpert = 1;       // ExpertEncoding id

    private readonly ILogger<CffSidGidMapper> _logger;

    public CffSidGidMapper(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CffSidGidMapper>();
    }

    /// <summary>
    /// Attempt to parse a name-keyed (non-CID) CFF font and produce glyph mapping metadata.
    /// Returns false on any structural parse failure or if the font is CID-keyed.
    /// </summary>
    /// <param name="cffDataMemory">Raw CFF table bytes.</param>
    /// <param name="info">Resulting mapping information.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public bool TryParseNameKeyed(ReadOnlyMemory<byte> cffDataMemory, out CffInfo info)
    {
        var cffBytes = cffDataMemory;
        info = null;

        try
        {
            var reader = new CffDataReader(cffBytes.Span);

            // Header
            if (!reader.TryReadByte(out _))
            {
                _logger.LogWarning("Failed to read CFF header: missing major version byte.");
                return false; // major
            }
            if (!reader.TryReadByte(out _))
            {
                _logger.LogWarning("Failed to read CFF header: missing minor version byte.");
                return false; // minor
            }
            if (!reader.TryReadByte(out byte headerSize))
            {
                _logger.LogWarning("Failed to read CFF header: missing header size byte.");
                return false;
            }
            if (!reader.TryReadByte(out _))
            {
                _logger.LogWarning("Failed to read CFF header: missing offSize byte.");
                return false; // offSize
            }

            // Name INDEX
            reader.Position = headerSize;
            if (!CffIndexReader.TryReadIndex(ref reader, out int nameIndexCount, out int nameIndexDataStart, out int[] nameIndexOffsets, out int topDictIndexStart))
            {
                _logger.LogWarning("Failed to read CFF Name INDEX.");
                return false;
            }

            // Top DICT INDEX
            reader.Position = topDictIndexStart;
            if (!CffIndexReader.TryReadIndex(ref reader, out int topDictCount, out int topDictDataStart, out int[] topDictOffsets, out int stringIndexStart))
            {
                _logger.LogWarning("Failed to read CFF Top DICT INDEX.");
                return false;
            }
            if (topDictCount < 1)
            {
                _logger.LogWarning("CFF Top DICT INDEX contains no dictionaries.");
                return false;
            }

            if (topDictCount > 1)
            {
                LogMultipleTopDicts(nameIndexCount, nameIndexDataStart, nameIndexOffsets, cffBytes.Span, topDictCount);
            }

            // Use first Top DICT
            var topDictStart = topDictDataStart + (topDictOffsets[0] - 1);
            var topDictEnd = topDictDataStart + (topDictOffsets[1] - 1);
            if (topDictStart < 0 || topDictEnd > cffBytes.Length || topDictEnd <= topDictStart)
            {
                _logger.LogWarning("Invalid Top DICT range: start={TopDictStart}, end={TopDictEnd}, length={CffLength}.", topDictStart, topDictEnd, cffBytes.Length);
                return false;
            }
            var topDictBytes = cffBytes.Slice(topDictStart, topDictEnd - topDictStart);

            var topDictReader = new CffTopDictReader();
            CffTopDictData topDictData = topDictReader.ParseTopDict(topDictBytes.Span);

            if (!topDictData.CharStringsOffset.HasValue || topDictData.CharStringsOffset.Value >= cffBytes.Length)
            {
                _logger.LogWarning("Top DICT missing or invalid CharStrings offset.");
                return false;
            }

            // CharStrings INDEX (determine glyph count)
            var charStringsReader = new CffDataReader(cffBytes.Span)
            {
                Position = topDictData.CharStringsOffset.Value
            };
            if (!CffIndexReader.TryReadIndex(ref charStringsReader, out int glyphCount, out _, out int[] _, out _))
            {
                _logger.LogWarning("Failed to read CharStrings INDEX.");
                return false;
            }
            if (glyphCount <= 0)
            {
                _logger.LogWarning("CharStrings INDEX contains no glyphs.");
                return false;
            }

            // Parse Private DICT for default/nominal widths
            double defaultWidthX = 0;
            double nominalWidthX = 0;
            if (topDictData.PrivateDictOffset.HasValue && topDictData.PrivateDictSize.HasValue)
            {
                int privateDictStart = topDictData.PrivateDictOffset.Value;
                int privateDictSize = topDictData.PrivateDictSize.Value;
                if (privateDictStart >= 0 && privateDictStart + privateDictSize <= cffBytes.Length)
                {
                    var privateDictBytes = cffBytes.Slice(privateDictStart, privateDictSize);
                    var privateDictParser = new CffPrivateDictParser();
                    CffPrivateDictData privateDictData = privateDictParser.ParsePrivateDict(privateDictBytes.Span);

                    defaultWidthX = privateDictData.DefaultWidthX ?? 0;
                    nominalWidthX = privateDictData.NominalWidthX ?? 0;
                }
            }

            // Parse charstring metrics
            var metricsParser = new CffCharStringMetricsParser();
            if (!metricsParser.TryParseCharStringMetrics(cffBytes.Span, topDictData.CharStringsOffset.Value, glyphCount, out CffCharacterMetrics[] charMetrics))
            {
                _logger.LogWarning("Failed to parse charstring metrics.");
                return false;
            }

            // Get font matrix for width transformation
            double fontMatrixScaleX = 0.001;
            if (topDictData.FontMatrix != null && topDictData.FontMatrix.Length >= 1)
            {
                fontMatrixScaleX = (double)topDictData.FontMatrix[0];
            }

            // Compute final widths: actualWidth = (nominalWidthX + width) * fontMatrixScaleX (or defaultWidthX * fontMatrixScaleX if width not specified)
            var gidWidths = new float[glyphCount];
            for (int gid = 0; gid < glyphCount; gid++)
            {
                double glyphSpaceWidth;
                if (charMetrics[gid].Width.HasValue)
                {
                    glyphSpaceWidth = nominalWidthX + charMetrics[gid].Width.Value;
                }
                else
                {
                    glyphSpaceWidth = defaultWidthX;
                }

                gidWidths[gid] = (float)(glyphSpaceWidth * fontMatrixScaleX);
            }

            // Charset -> SID list
            var charsetParser = new CffCharsetParser();
            if (!topDictData.CharsetOffset.HasValue || !charsetParser.TryParseCharset(cffBytes.Span, topDictData.CharsetOffset.Value, glyphCount, out ushort[] sidByGlyph))
            {
                _logger.LogWarning("Failed to parse CFF charset or missing charset offset.");
                return false;
            }

            // String INDEX (custom strings after StandardStrings)
            var stringIndexReader = new CffDataReader(cffBytes.Span)
            {
                Position = stringIndexStart
            };
            if (!CffIndexReader.TryReadIndex(ref stringIndexReader, out int stringIndexCount, out int stringIndexDataStart, out int[] stringIndexOffsets, out _))
            {
                _logger.LogWarning("Failed to read CFF String INDEX.");
                return false;
            }
            var customStrings = new PdfString[stringIndexCount];
            for (int stringIndex = 0; stringIndex < stringIndexCount; stringIndex++)
            {
                int start = stringIndexDataStart + (stringIndexOffsets[stringIndex] - 1);
                int end = stringIndexDataStart + (stringIndexOffsets[stringIndex + 1] - 1);
                if (start < 0 || end < start || end > cffBytes.Length)
                {
                    customStrings[stringIndex] = default;
                    continue;
                }
                var slice = cffBytes.Slice(start, end - start);
                customStrings[stringIndex] = slice;
            }

            // Build name->GID & SID->GID maps
            var glyphNameToGid = new Dictionary<PdfString, ushort>(glyphCount);
            for (ushort glyphId = 0; glyphId < sidByGlyph.Length; glyphId++)
            {
                ushort sid = sidByGlyph[glyphId];

                PdfString glyphName = ResolveGlyphName(sid, customStrings);
                if (!glyphName.IsEmpty && !glyphNameToGid.ContainsKey(glyphName))
                {
                    glyphNameToGid[glyphName] = glyphId;
                }
            }

            // TODO: [MEDIUM] we could also parse the Encoding table here for code->GID mapping.

            PdfFontEncoding encoding = topDictData.EncodingOffset.GetValueOrDefault() switch
            {
                PredefinedEncodingStandard => PdfFontEncoding.StandardEncoding,
                PredefinedEncodingExpert => PdfFontEncoding.MacExpertEncoding,
                _ => PdfFontEncoding.Unknown
            };

            info = new CffInfo
            {
                GlyphCount = glyphCount,
                NameToGid = glyphNameToGid,
                IsCidFont = topDictData.IsCidKeyed,
                GidToSid = sidByGlyph,
                GidWidths = gidWidths,
                Encoding = encoding,
                CffData = cffDataMemory
            };

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse CFF name-keyed data.");
            info = null;
            return false;
        }
    }

    private void LogMultipleTopDicts(int nameIndexCount, int nameIndexDataStart, int[] nameIndexOffsets, ReadOnlySpan<byte> cffBytes, int topDictCount)
    {
        try
        {
            var topNames = new List<string>(nameIndexCount);
            for (int nameIndex = 0; nameIndex < nameIndexCount; nameIndex++)
            {
                int start = nameIndexDataStart + (nameIndexOffsets[nameIndex] - 1);
                int end = nameIndexDataStart + (nameIndexOffsets[nameIndex + 1] - 1);
                if (start >= 0 && end >= start && end <= cffBytes.Length)
                {
                    var slice = cffBytes.Slice(start, end - start);
                    topNames.Add(Encoding.ASCII.GetString(slice));
                }
            }

            if (topNames.Count > 0)
            {
                _logger.LogInformation("CFF contains {TopDictCount} Top DICTs (fonts): {FontNames}. Using the first one.", topDictCount, string.Join(", ", topNames));
            }
            else
            {
                _logger.LogInformation("CFF contains {TopDictCount} Top DICTs (fonts). Using the first one.", topDictCount);
            }
        }
        catch
        {
            // Safe to ignore logging errors here.
            _logger.LogInformation("CFF contains {TopDictCount} Top DICTs (fonts). Using the first one.", topDictCount);
        }
    }

    private static PdfString ResolveGlyphName(ushort sid, PdfString[] customStrings)
    {
        if (sid < CffData.StandardStrings.Length)
        {
            return CffData.StandardStrings[sid];
        }

        int customIndex = sid - CffData.StandardStrings.Length;
        if ((uint)customIndex < (uint)customStrings.Length)
        {
            return customStrings[customIndex];
        }

        return default;
    }
}
