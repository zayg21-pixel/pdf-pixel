using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Model;
using SkiaSharp;

namespace PdfPixel.Fonts.Cff;

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
    public static byte[]? Wrap(PdfFontDescriptor? descriptor, CffInfo? cffInfo)
    {
        if (cffInfo == null)
        {
            return null;
        }

        if (descriptor == null)
        {
            return null;
        }

        ReadOnlyMemory<byte> cffData = cffInfo.CffData;
        if (cffData.IsEmpty)
        {
            return null;
        }

        var numGlyphs = (ushort)Math.Max(1, Math.Min(ushort.MaxValue, cffInfo.GlyphCount));

        List<Table> tables = [
            MakeTable(TagCff, cffData.ToArray()),
            MakeTable(TagMaxp, BuildMaxp(numGlyphs)),
            MakeTable(TagOS2, BuildOS2(numGlyphs, descriptor)),
            MakeTable(TagHhea, BuildHhea(numGlyphs, descriptor)),
            MakeTable(TagHmtx, BuildHmtx(numGlyphs, cffInfo.GidWidths)),
            MakeTable(TagPost, BuildPost())
        ];

        tables.Add(MakeTable(TagName, BuildName(descriptor)));
        tables.Add(MakeTable(TagCmap, BuildCmapEmpty()));
        tables.Add(MakeTable(TagHead, BuildHead(checksumAdjustment: 0, descriptor))); // Add last – checksum patched later.

        tables.Sort((left, right) => left.Tag.CompareTo(right.Tag));

        var tableCount = (ushort)tables.Count;
        CffOpenTypeWriter.ComputeDirParams(tableCount, out ushort searchRange, out ushort entrySelector, out ushort rangeShift);

        int offset = 12 + (tableCount * 16); // header + directory entries
        for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            Table table = tables[tableIndex];
            offset = CffOpenTypeWriter.Align4(offset);
            table.Offset = offset;
            table.Checksum = CffOpenTypeWriter.CalcTableChecksum(table.Data);
            tables[tableIndex] = table;
            offset += CffOpenTypeWriter.Align4(table.Data.Length);
        }

        // First pass build with checksumAdjustment = 0 to compute whole font checksum.
        byte[] firstPassFont = BuildFontBytes(tables, tableCount, searchRange, entrySelector, rangeShift);
        uint totalChecksum = CffOpenTypeWriter.CalcTableChecksum(firstPassFont);
        uint checksumAdjustment = unchecked(ChecksumMagic - totalChecksum);

        // Patch head checksumAdjustment and its checksum.
        for (int i = 0; i < tables.Count; i++)
        {
            if (tables[i].Tag == TagHead)
            {
                Table headTable = tables[i];
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
        return new()
        {
            Tag = tag,
            Data = data,
            Checksum = 0,
            Offset = 0
        };
    }

    private static byte[] BuildHead(uint checksumAdjustment, PdfFontDescriptor fontDescriptor)
    {
        using (MemoryStream stream = new(54))
        using (BinaryWriter writer = new(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00010000);        // version 1.0
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00010000);        // fontRevision 1.0
            CffOpenTypeWriter.WriteUInt32BE(writer, checksumAdjustment);
            CffOpenTypeWriter.WriteUInt32BE(writer, HeadMagic);
            CffOpenTypeWriter.WriteUInt16BE(writer, 0x000B);            // flags (baseline/lsb/pointsize)
            CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)DefaultUnitsPerEm); // unitsPerEm
            CffOpenTypeWriter.WriteInt64BE(writer, 0);                  // created
            CffOpenTypeWriter.WriteInt64BE(writer, 0);                  // modified
            CffOpenTypeWriter.WriteInt16BE(writer, (short)Math.Min(0, Math.Round(fontDescriptor.FontBBox.Left))); // xMin
            CffOpenTypeWriter.WriteInt16BE(writer, CffOpenTypeWriter.ClampToShort(fontDescriptor.Descent, DefaultDescent)); // yMin
            CffOpenTypeWriter.WriteInt16BE(writer, (short)Math.Max(DefaultUnitsPerEm, Math.Round(fontDescriptor.FontBBox.Right))); // xMax
            CffOpenTypeWriter.WriteInt16BE(writer, CffOpenTypeWriter.ClampToShort(fontDescriptor.Ascent, DefaultAscent)); // yMax
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
        using (MemoryStream stream = new(6))
        using (BinaryWriter writer = new(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00005000); // version 0.5 for CFF
            CffOpenTypeWriter.WriteUInt16BE(writer, numGlyphs);
            return stream.ToArray();
        }
    }

    private static byte[] BuildHhea(ushort numGlyphs, PdfFontDescriptor fontDescriptor)
    {
        using (MemoryStream stream = new(36))
        using (BinaryWriter writer = new(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, 0x00010000); // version 1.0
            CffOpenTypeWriter.WriteInt16BE(writer, CffOpenTypeWriter.ClampToShort(fontDescriptor.Ascent, DefaultAscent));
            CffOpenTypeWriter.WriteInt16BE(writer, CffOpenTypeWriter.ClampToShort(fontDescriptor.Descent, DefaultDescent));
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultLineGap); // line gap
            CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)Math.Max(1, (int)Math.Round((fontDescriptor.MaxWidth != 0) ? fontDescriptor.MaxWidth : DefaultAdvanceWidth))); // advanceWidthMax
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
            CffOpenTypeWriter.WriteUInt16BE(writer, numGlyphs); // numberOfHMetrics: one real entry per glyph, no compression.
            return stream.ToArray();
        }
    }

    private static byte[] BuildHmtx(ushort numGlyphs, float[]? gidWidths)
    {
        using (MemoryStream stream = new(numGlyphs * 4))
        using (BinaryWriter writer = new(stream))
        {
            for (int glyphId = 0; glyphId < numGlyphs; glyphId++)
            {
                float glyphSpaceWidth = (gidWidths != null && glyphId < gidWidths.Length) ? gidWidths[glyphId] : 0;
                var advanceWidth = (ushort)Math.Max(0, Math.Round(glyphSpaceWidth * DefaultUnitsPerEm));
                CffOpenTypeWriter.WriteUInt16BE(writer, advanceWidth);
                CffOpenTypeWriter.WriteInt16BE(writer, 0); // lsb
            }

            return stream.ToArray();
        }
    }

    private static byte[] BuildPost()
    {
        using (MemoryStream stream = new(32))
        using (BinaryWriter writer = new(stream))
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
        using (MemoryStream stream = new(78))
        using (BinaryWriter writer = new(stream))
        {
            short xAvgCharWidth = CffOpenTypeWriter.ClampToShort((fontDescriptor.AvgWidth != 0) ? fontDescriptor.AvgWidth : DefaultAvgWidth, DefaultAvgWidth);
            var usWeightClass = (ushort)((fontDescriptor.FontWeight >= 100 && fontDescriptor.FontWeight <= 900)
                ? fontDescriptor.FontWeight
                : ((fontDescriptor.Flags & PdfFontFlags.ForceBold) == PdfFontFlags.ForceBold) ? DefaultBoldWeight : DefaultWeightIfUnknown);
            const ushort usWidthClass = 5; // Medium width
            short sTypoAscender = CffOpenTypeWriter.ClampToShort((fontDescriptor.Ascent != 0) ? fontDescriptor.Ascent : DefaultAscent, DefaultAscent);
            short sTypoDescender = CffOpenTypeWriter.ClampToShort((fontDescriptor.Descent != 0) ? fontDescriptor.Descent : DefaultDescent, DefaultDescent);
            const short sTypoLineGap = DefaultLineGap;
            bool hasFontBBox = fontDescriptor.FontBBox != SKRect.Empty;
            var usWinAscent = (ushort)Math.Max(0, (int)Math.Round(hasFontBBox ? fontDescriptor.FontBBox.Bottom : DefaultAscent));
            var usWinDescent = (ushort)Math.Max(0, -(int)Math.Round(hasFontBBox ? fontDescriptor.FontBBox.Top : DefaultDescent));

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
            CffOpenTypeWriter.WriteInt16BE(writer, 2);
            CffOpenTypeWriter.WriteInt16BE(writer, -1); // ySubscript
            CffOpenTypeWriter.WriteInt16BE(writer, 0);
            CffOpenTypeWriter.WriteInt16BE(writer, 0);
            CffOpenTypeWriter.WriteInt16BE(writer, 2);
            CffOpenTypeWriter.WriteInt16BE(writer, 1);  // ySuperscript
            CffOpenTypeWriter.WriteInt16BE(writer, 0);
            CffOpenTypeWriter.WriteInt16BE(writer, 0);
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultStrikeoutSize);
            CffOpenTypeWriter.WriteInt16BE(writer, DefaultStrikeoutPosition);
            CffOpenTypeWriter.WriteInt16BE(writer, 0); // sFamilyClass
            byte[]? panose = fontDescriptor.Panose;
            for (int i = 0; i < 10; i++)
            {
                writer.Write((byte)((panose != null && i < panose.Length) ? panose[i] : 0));
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
        var parsed = PdfSubstitutionInfo.Parse(descriptor.FontName, descriptor);

        string family = parsed.NormalizedStem;
        if (string.IsNullOrWhiteSpace(family))
        {
            family = DefaultFamilyBaseName;
        }

        bool boldHint = parsed.IsBold;
        bool italicHint = parsed.IsItalic;

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

        string baseName = parsed.NormalizedStem;
        string postScriptName = baseName.Replace(' ', '-');

        List<(ushort NameId, string Value)> records = [
            (NameIdFontFamily, family),
            (NameIdFontSubfamily, subfamily),
            (NameIdFullFontName, $"{family} {subfamily}"),
            (NameIdPostScriptName, postScriptName),
            (NameIdVersionString, DefaultVersionString)
        ];

        const ushort platformWindows = 3;
        const ushort encodingUnicodeBmp = 1;
        const ushort languageEnUs = 0x0409;

        List<byte[]> stringData = [];
        for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
        {
            stringData.Add(Encoding.BigEndianUnicode.GetBytes(records[recordIndex].Value));
        }

        var recordCount = (ushort)records.Count;
        int storageOffset = 6 + (recordCount * 12);

        using (MemoryStream stream = new())
        using (BinaryWriter writer = new(stream))
        {
            CffOpenTypeWriter.WriteUInt16BE(writer, 0);
            CffOpenTypeWriter.WriteUInt16BE(writer, recordCount);
            CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)storageOffset);

            int currentOffset = 0;
            for (int i = 0; i < recordCount; i++)
            {
                CffOpenTypeWriter.WriteUInt16BE(writer, platformWindows);
                CffOpenTypeWriter.WriteUInt16BE(writer, encodingUnicodeBmp);
                CffOpenTypeWriter.WriteUInt16BE(writer, languageEnUs);
                CffOpenTypeWriter.WriteUInt16BE(writer, records[i].NameId);
                CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)stringData[i].Length);
                CffOpenTypeWriter.WriteUInt16BE(writer, (ushort)currentOffset);
                currentOffset += stringData[i].Length;
            }

            for (int i = 0; i < stringData.Count; i++)
            {
                byte[] bytes = stringData[i];
                writer.Write(bytes, 0, bytes.Length);
            }

            return stream.ToArray();
        }
    }

    private static byte[] BuildCmapEmpty()
    {
        using (MemoryStream stream = new())
        using (BinaryWriter writer = new(stream))
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
        using (MemoryStream stream = new())
        using (BinaryWriter writer = new(stream))
        {
            CffOpenTypeWriter.WriteUInt32BE(writer, SfntVersion);
            CffOpenTypeWriter.WriteUInt16BE(writer, numTables);
            CffOpenTypeWriter.WriteUInt16BE(writer, searchRange);
            CffOpenTypeWriter.WriteUInt16BE(writer, entrySelector);
            CffOpenTypeWriter.WriteUInt16BE(writer, rangeShift);

            for (int i = 0; i < tables.Count; i++)
            {
                Table table = tables[i];
                CffOpenTypeWriter.WriteUInt32BE(writer, table.Tag);
                CffOpenTypeWriter.WriteUInt32BE(writer, table.Checksum);
                CffOpenTypeWriter.WriteUInt32BE(writer, (uint)table.Offset);
                CffOpenTypeWriter.WriteUInt32BE(writer, (uint)table.Data.Length);
            }

            for (int i = 0; i < tables.Count; i++)
            {
                Table table = tables[i];
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
