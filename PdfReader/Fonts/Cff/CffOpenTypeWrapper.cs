using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Types;

namespace PdfReader.Fonts.Cff;

/// <summary>
/// Wraps raw CFF (Type 1C) font data in a minimal OpenType (OTTO) container, synthesizing mandatory tables.
/// The wrapper is intentionally minimal and only aims to satisfy typical FreeType / Skia requirements.
/// </summary>
internal static class CffOpenTypeWrapper
{
    private const uint SfntVersion = 0x4F54544F; // 'OTTO'
    private const uint TagCff = 0x43464620;      // 'CFF '
    private const uint TagHead = 0x68656164;     // 'head'
    private const uint TagMaxp = 0x6D617870;     // 'maxp'
    private const uint TagHhea = 0x68686561;     // 'hhea'
    private const uint TagHmtx = 0x686D7478;     // 'hmtx'
    private const uint TagPost = 0x706F7374;     // 'post'
    private const uint TagOS2 = 0x4F532F32;      // 'OS/2'
    private const uint TagName = 0x6E616D65;     // 'name'
    private const uint TagCmap = 0x636D6170;     // 'cmap'

    private const uint HeadMagic = 0x5F0F3CF5;
    private const uint ChecksumMagic = 0xB1B0AFBA;

    private const ushort NameIdFontFamily = 1;
    private const ushort NameIdFontSubfamily = 2;
    private const ushort NameIdFullFontName = 4;
    private const ushort NameIdPostScriptName = 6;
    private const ushort NameIdVersionString = 5;

    private const short DefaultAscent = 800;
    private const short DefaultDescent = -200;
    private const short DefaultUnitsPerEm = 1000;
    private const ushort DefaultAdvanceWidth = 1000;
    private const short DefaultUnderlineThickness = 50;
    private const short DefaultStrikeoutSize = 10;
    private const short DefaultStrikeoutPosition = 250;
    private const ushort DefaultLowestRecPpem = 8;
    private const short DefaultCaretSlopeRise = 1;
    private const short DefaultCaretSlopeRun = 0;
    private const short DefaultLineGap = 0;
    private const ushort DefaultWeightIfUnknown = 400;
    private const ushort DefaultBoldWeight = 700;
    private const short DefaultAvgWidth = 500;
    private const string DefaultFamilyBaseName = "CFFWrapped";
    private const string DefaultVersionString = "Version 1.0";

    private struct Table
    {
        public uint Tag;
        public byte[] Data;
        public uint Checksum;
        public int Offset;
    }

    /// <summary>
    /// Produce a minimal OpenType font byte array containing the supplied CFF data and synthetic tables.
    /// Returns null if input is invalid or empty.
    /// </summary>
    public static byte[] Wrap(PdfFontDescriptor descriptor, CffInfo cffInfo)
    {
        if (cffInfo == null)
        {
            return null;
        }
        if (descriptor == null)
        {
            return null;
        }

        var cffData = cffInfo.CffData;
        if (cffData.IsEmpty)
        {
            return null;
        }

        ushort numGlyphs = (ushort)Math.Max(1, Math.Min(ushort.MaxValue, cffInfo.GlyphCount));

        var tables = new List<Table>
        {
            MakeTable(TagCff, cffData.ToArray()),
            MakeTable(TagMaxp, BuildMaxp(numGlyphs)),
            MakeTable(TagOS2, BuildOS2(numGlyphs, descriptor)),
            MakeTable(TagHhea, BuildHhea(numberOfHMetrics: 1, descriptor)),
            MakeTable(TagHmtx, BuildHmtx(numGlyphs, numberOfHMetrics: 1)),
            MakeTable(TagPost, BuildPost())
        };

        tables.Add(MakeTable(TagName, BuildName(descriptor)));
        tables.Add(MakeTable(TagCmap, BuildCmapEmpty()));
        tables.Add(MakeTable(TagHead, BuildHead(checksumAdjustment: 0))); // Add last – checksum patched later.

        tables.Sort((left, right) => left.Tag.CompareTo(right.Tag));

        ushort tableCount = (ushort)tables.Count;
        CffOpenTypeWriter.ComputeDirParams(tableCount, out ushort searchRange, out ushort entrySelector, out ushort rangeShift);

        int offset = 12 + tableCount * 16; // header + directory entries
        for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            var table = tables[tableIndex];
            offset = CffOpenTypeWriter.Align4(offset);
            table.Offset = offset;
            table.Checksum = CffOpenTypeWriter.CalcTableChecksum(table.Data);
            tables[tableIndex] = table;
            offset += CffOpenTypeWriter.Align4(table.Data.Length);
        }

        // First pass build with checksumAdjustment = 0 to compute whole font checksum.
        var firstPassFont = BuildFontBytes(tables, tableCount, searchRange, entrySelector, rangeShift);
        uint totalChecksum = CffOpenTypeWriter.CalcTableChecksum(firstPassFont);
        uint checksumAdjustment = unchecked(ChecksumMagic - totalChecksum);

        // Patch head checksumAdjustment and its checksum.
        for (int i = 0; i < tables.Count; i++)
        {
            if (tables[i].Tag == TagHead)
            {
                var headTable = tables[i];
                CffOpenTypeWriter.WriteUInt32BE(headTable.Data, 8, checksumAdjustment); // checksumAdjustment field offset.
                headTable.Checksum = CffOpenTypeWriter.CalcTableChecksum(headTable.Data);
                tables[i] = headTable;
                break;
            }
        }

        return BuildFontBytes(tables, tableCount, searchRange, entrySelector, rangeShift);
    }

    private static Table MakeTable(uint tag, byte[] data)
    {
        return new Table
        {
            Tag = tag,
            Data = data,
            Checksum = 0,
            Offset = 0
        };
    }

    private static byte[] BuildHead(uint checksumAdjustment)
    {
        using (var stream = new MemoryStream(54))
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00010000);        // version 1.0
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00010000);        // fontRevision 1.0
            CffOpenTypeWriter.WriteUInt32BE(writer, checksumAdjustment);
            CffOpenTypeWriter.WriteUInt32BE(writer, HeadMagic);
            CffOpenTypeWriter.WriteUInt16BE(writer, 0x000B);            // flags (baseline/lsb/pointsize)
            CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)DefaultUnitsPerEm); // unitsPerEm
            CffOpenTypeWriter.WriteInt64BE(writer, 0);                  // created
            CffOpenTypeWriter.WriteInt64BE(writer, 0);                  // modified
            CffOpenTypeWriter.WriteInt16BE(writer, 0);                  // xMin
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultDescent);     // yMin
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultUnitsPerEm);  // xMax (approx)
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultAscent);      // yMax (approx)
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);                 // macStyle
            CffOpenTypeWriter.WriteUInt16BE(writer, DefaultLowestRecPpem);
            CffOpenTypeWriter.WriteInt16BE(writer, 2);                  // fontDirectionHint
            CffOpenTypeWriter.WriteInt16BE(writer, 0);                  // indexToLocFormat
            CffOpenTypeWriter.WriteInt16BE(writer, 0);                  // glyphDataFormat
            return stream.ToArray();
        }
    }

    private static byte[] BuildMaxp(ushort numGlyphs)
    {
        using (var stream = new MemoryStream(6))
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00005000); // version 0.5 for CFF
            CffOpenTypeWriter.WriteUInt16BE(writer, numGlyphs);
            return stream.ToArray();
        }
    }

    private static byte[] BuildHhea(ushort numberOfHMetrics, PdfFontDescriptor fontDescriptor)
    {
        using (var stream = new MemoryStream(36))
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00010000); // version 1.0
            CffOpenTypeWriter.WriteInt16BE(writer, CffOpenTypeWriter.ClampToShort(fontDescriptor.Ascent, DefaultAscent));
            CffOpenTypeWriter.WriteInt16BE(writer, CffOpenTypeWriter.ClampToShort(fontDescriptor.Descent, DefaultDescent));
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultLineGap); // line gap
            CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)Math.Max(1, (int)Math.Round(fontDescriptor.MaxWidth != 0 ? fontDescriptor.MaxWidth : DefaultAdvanceWidth))); // advanceWidthMax
            CffOpenTypeWriter.WriteInt16BE(writer, 0); // minLeftSideBearing
            CffOpenTypeWriter.WriteInt16BE(writer, 0); // minRightSideBearing
            CffOpenTypeWriter.WriteInt16BE(writer, (short)Math.Max(0, (int)Math.Round(fontDescriptor.FontBBox.Right - fontDescriptor.FontBBox.Left))); // xMaxExtent
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultCaretSlopeRise);
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultCaretSlopeRun);
            for (int reservedIndex = 0; reservedIndex < 5; reservedIndex++)
            {
                CffOpenTypeWriter.WriteInt16BE(writer, 0);
            }
            CffOpenTypeWriter.WriteInt16BE(writer, 0); // metricDataFormat
            CffOpenTypeWriter.WriteUInt16BE(writer, numberOfHMetrics);
            return stream.ToArray();
        }
    }

    private static byte[] BuildHmtx(ushort numGlyphs, ushort numberOfHMetrics)
    {
        int length = numberOfHMetrics * 4 + Math.Max(0, numGlyphs - numberOfHMetrics) * 2;
        using (var stream = new MemoryStream(length))
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt16BE(writer, DefaultAdvanceWidth); // single metric: advance
            CffOpenTypeWriter.WriteInt16BE(writer, 0);                    // lsb
            for (int glyphIndex = 0; glyphIndex < numGlyphs - numberOfHMetrics; glyphIndex++)
            {
                CffOpenTypeWriter.WriteInt16BE(writer, 0);
            }
            return stream.ToArray();
        }
    }

    private static byte[] BuildPost()
    {
        using (var stream = new MemoryStream(32))
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00030000); // version 3.0 (no glyph names)
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);          // italicAngle
            CffOpenTypeWriter.WriteInt16BE(writer, 0);           // underlinePosition
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultUnderlineThickness);
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);          // isFixedPitch
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);          // minMemType42
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);          // maxMemType42
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);          // minMemType1
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);          // maxMemType1
            return stream.ToArray();
        }
    }

    private static byte[] BuildOS2(ushort numGlyphs, PdfFontDescriptor fontDescriptor)
    {
        using (var stream = new MemoryStream(78))
        using (var writer = new BinaryWriter(stream))
        {
            short xAvgCharWidth = CffOpenTypeWriter.ClampToShort(fontDescriptor.AvgWidth != 0 ? fontDescriptor.AvgWidth : DefaultAvgWidth, DefaultAvgWidth);
            ushort usWeightClass = (ushort)(fontDescriptor.FontWeight >= 100 && fontDescriptor.FontWeight <= 900 ? fontDescriptor.FontWeight : (fontDescriptor.Flags & PdfFontFlags.ForceBold) == PdfFontFlags.ForceBold ? DefaultBoldWeight : DefaultWeightIfUnknown);
            ushort usWidthClass = 5; // Medium width
            short sTypoAscender = CffOpenTypeWriter.ClampToShort(fontDescriptor.Ascent != 0 ? fontDescriptor.Ascent : DefaultAscent, DefaultAscent);
            short sTypoDescender = CffOpenTypeWriter.ClampToShort(fontDescriptor.Descent != 0 ? fontDescriptor.Descent : DefaultDescent, DefaultDescent);
            short sTypoLineGap = DefaultLineGap;
            ushort usWinAscent = (ushort)Math.Max(0, (int)Math.Round(fontDescriptor.FontBBox.Top != 0 ? fontDescriptor.FontBBox.Top : DefaultAscent));
            ushort usWinDescent = (ushort)Math.Max(0, -(int)Math.Round(fontDescriptor.FontBBox.Bottom != 0 ? fontDescriptor.FontBBox.Bottom : DefaultDescent));

            ushort fsSelection = 0;
            bool italic = (fontDescriptor.Flags & PdfFontFlags.Italic) == PdfFontFlags.Italic || Math.Abs(fontDescriptor.ItalicAngle) > 0.1f;
            bool bold = usWeightClass >= DefaultBoldWeight;
            if (italic)
            {
                fsSelection |= 0x0001;
            }
            if (bold)
            {
                fsSelection |= 0x0020;
            }
            if (!italic && !bold)
            {
                fsSelection |= 0x0002; // REGULAR
            }

            CffOpenTypeWriter.WriteUInt16BE(writer, 0);              // version 0
            CffOpenTypeWriter.WriteInt16BE(writer, xAvgCharWidth);   // xAvgCharWidth
            CffOpenTypeWriter.WriteUInt16BE(writer, usWeightClass);  // usWeightClass
            CffOpenTypeWriter.WriteUInt16BE(writer, usWidthClass);   // usWidthClass
            CffOpenTypeWriter.WriteInt16BE(writer, 0);               // fsType
            CffOpenTypeWriter.WriteInt16BE(writer, 2); CffOpenTypeWriter.WriteInt16BE(writer, -1); // ySubscript
            CffOpenTypeWriter.WriteInt16BE(writer, 0); CffOpenTypeWriter.WriteInt16BE(writer, 0);
            CffOpenTypeWriter.WriteInt16BE(writer, 2); CffOpenTypeWriter.WriteInt16BE(writer, 1);  // ySuperscript
            CffOpenTypeWriter.WriteInt16BE(writer, 0); CffOpenTypeWriter.WriteInt16BE(writer, 0);
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultStrikeoutSize);
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultStrikeoutPosition);
            CffOpenTypeWriter.WriteInt16BE(writer, 0); // sFamilyClass
            var panose = fontDescriptor.Panose;
            for (int i = 0; i < 10; i++)
            {
                writer.Write((byte)(panose != null && i < panose.Length ? panose[i] : 0));
            }
            for (int rangeIndex = 0; rangeIndex < 4; rangeIndex++)
            {
                CffOpenTypeWriter.WriteUInt32BE(writer, 0); // Unicode ranges
            }
            CffOpenTypeWriter.WriteUInt32BE(writer, 0);      // achVendID
            CffOpenTypeWriter.WriteUInt16BE(writer, fsSelection);
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);      // usFirstCharIndex
            CffOpenTypeWriter.WriteUInt16BE(writer, 0xFFFF); // usLastCharIndex (placeholder span)
            CffOpenTypeWriter.WriteInt16BE(writer, sTypoAscender);
            CffOpenTypeWriter.WriteInt16BE(writer, sTypoDescender);
            CffOpenTypeWriter.WriteInt16BE(writer, sTypoLineGap);
            CffOpenTypeWriter.WriteUInt16BE(writer, usWinAscent);
            CffOpenTypeWriter.WriteUInt16BE(writer, usWinDescent);
            return stream.ToArray();
        }
    }

    private static byte[] BuildName(PdfFontDescriptor descriptor)
    {
        var parsed = PdfFontName.Parse(descriptor?.FontName ?? default);

        string family = parsed.NormalizedStem;
        if (string.IsNullOrWhiteSpace(family))
        {
            family = DefaultFamilyBaseName;
        }

        bool boldHint = parsed.BoldHint;
        bool italicHint = parsed.ItalicHint;

        string subfamily;
        if (boldHint && italicHint)
        {
            subfamily = "Bold Italic";
        }
        else if (boldHint)
        {
            subfamily = "Bold";
        }
        else if (italicHint)
        {
            subfamily = "Italic";
        }
        else
        {
            subfamily = "Regular";
        }

        string baseNameNoSubset = parsed.RawName.ToString();
        if (!string.IsNullOrEmpty(parsed.SubsetTag) && baseNameNoSubset.Length > parsed.SubsetTag.Length + 1)
        {
            int plusIndex = baseNameNoSubset.IndexOf('+');
            if (plusIndex >= 0 && plusIndex < baseNameNoSubset.Length - 1)
            {
                baseNameNoSubset = baseNameNoSubset.Substring(plusIndex + 1);
            }
        }
        if (string.IsNullOrEmpty(baseNameNoSubset))
        {
            baseNameNoSubset = family;
        }
        string postScriptName = baseNameNoSubset.Replace(' ', '-');

        var records = new List<(ushort NameId, string Value)>
        {
            (NameIdFontFamily, family),
            (NameIdFontSubfamily, subfamily),
            (NameIdFullFontName, string.Concat(family, " ", subfamily)),
            (NameIdPostScriptName, postScriptName),
            (NameIdVersionString, DefaultVersionString)
        };

        const ushort PlatformWindows = 3;
        const ushort EncodingUnicodeBmp = 1;
        const ushort LanguageEnUs = 0x0409;

        var stringData = new List<byte[]>();
        for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
        {
            stringData.Add(Encoding.BigEndianUnicode.GetBytes(records[recordIndex].Value));
        }

        ushort recordCount = (ushort)records.Count;
        int storageOffset = 6 + recordCount * 12;

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);
            CffOpenTypeWriter.WriteUInt16BE(writer, recordCount);
            CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)storageOffset);

            int currentOffset = 0;
            for (int i = 0; i < recordCount; i++)
            {
                CffOpenTypeWriter.WriteUInt16BE(writer, PlatformWindows);
                CffOpenTypeWriter.WriteUInt16BE(writer, EncodingUnicodeBmp);
                CffOpenTypeWriter.WriteUInt16BE(writer, LanguageEnUs);
                CffOpenTypeWriter.WriteUInt16BE(writer, records[i].NameId);
                CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)stringData[i].Length);
                CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)currentOffset);
                currentOffset += stringData[i].Length;
            }

            for (int i = 0; i < stringData.Count; i++)
            {
                var bytes = stringData[i];
                writer.Write(bytes, 0, bytes.Length);
            }

            return stream.ToArray();
        }
    }

    private static byte[] BuildCmapEmpty()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt16BE(writer, 0); // version
            CffOpenTypeWriter.WriteUInt16BE(writer, 1); // numTables
            CffOpenTypeWriter.WriteUInt16BE(writer, 3); // platformID (Windows)
            CffOpenTypeWriter.WriteUInt16BE(writer, 1); // encodingID (Unicode BMP)
            CffOpenTypeWriter.WriteUInt32BE(writer, 12); // offset to subtable

            // Subtable (format 4) – minimal terminating mapping.
            CffOpenTypeWriter.WriteUInt16BE(writer, 4);  // format
            CffOpenTypeWriter.WriteUInt16BE(writer, 16); // length
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);  // language
            CffOpenTypeWriter.WriteUInt16BE(writer, 2);  // segCountX2 (1 segment => 2)
            CffOpenTypeWriter.WriteUInt16BE(writer, 2);  // searchRange
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);  // entrySelector
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);  // rangeShift
            CffOpenTypeWriter.WriteUInt16BE(writer, 0xFFFF); // endCount
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);      // reservedPad
            CffOpenTypeWriter.WriteUInt16BE(writer, 0xFFFF); // startCount
            CffOpenTypeWriter.WriteUInt16BE(writer, 1);      // idDelta
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);      // idRangeOffset
            return stream.ToArray();
        }
    }

    private static byte[] BuildFontBytes(List<Table> tables, ushort numTables, ushort searchRange, ushort entrySelector, ushort rangeShift)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, SfntVersion);
            CffOpenTypeWriter.WriteUInt16BE(writer, numTables);
            CffOpenTypeWriter.WriteUInt16BE(writer, searchRange);
            CffOpenTypeWriter.WriteUInt16BE(writer, entrySelector);
            CffOpenTypeWriter.WriteUInt16BE(writer, rangeShift);

            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                CffOpenTypeWriter.WriteUInt32BE(writer, table.Tag);
                CffOpenTypeWriter.WriteUInt32BE(writer, table.Checksum);
                CffOpenTypeWriter.WriteUInt32BE(writer, (uint)table.Offset);
                CffOpenTypeWriter.WriteUInt32BE(writer, (uint)table.Data.Length);
            }

            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                while (stream.Position < table.Offset)
                {
                    writer.Write((byte)0);
                }
                writer.Write(table.Data, 0, table.Data.Length);
                int paddedLength = CffOpenTypeWriter.Align4(table.Data.Length);
                for (int pad = table.Data.Length; pad < paddedLength; pad++)
                {
                    writer.Write((byte)0);
                }
            }

            return stream.ToArray();
        }
    }
}
