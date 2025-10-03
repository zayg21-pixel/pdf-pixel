using HarfBuzzSharp;
using PdfReader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Attempts to wrap a bare CFF (Type 1C) blob into a minimally valid OpenType (SFNT/OTTO) font.
    /// Adds the following tables with synthetic defaults:
    /// - CFF  (provided)
    /// - head (checksum adjustment computed)
    /// - maxp (v0.5)
    /// - hhea/hmtx (basic horizontal metrics)
    /// - post (v3.0)
    /// - OS/2 (v0)
    /// - name (basic English names)
    /// - cmap (Windows Symbol or Unicode BMP depending on available mappings)
    /// This may still be rejected by some engines but improves chances with FreeType/Skia.
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
        private const uint TagOS2  = 0x4F532F32;     // 'OS/2'
        private const uint TagName = 0x6E616D65;     // 'name'
        private const uint TagCmap = 0x636D6170;     // 'cmap'
        private const uint HeadMagic = 0x5F0F3CF5;
        private const uint ChecksumMagic = 0xB1B0AFBA;

        private struct Table
        {
            public uint Tag;
            public byte[] Data;
            public uint Checksum;
            public int Offset;
        }

        public static byte[] Wrap(PdfFontDescriptor decriptor, CffNameKeyedInfo cffInfo)
        {
            var cffData = cffInfo.CffData;

            if (cffData.IsEmpty)
            {
                return null;
            }

            // Build cmap first and get mapped character count (to reuse for glyph count fallback)
            //var cmapBytes = BuildCmapFromCff(pdfFont, cffInfo);

            // Determine glyph count from CFF CharStrings if possible; fallback to cmapCharCount
            ushort numGlyphs = 1;
            int glyphCount = cffInfo?.GlyphCount ?? 0;
            numGlyphs = (ushort)Math.Max(1, Math.Min(ushort.MaxValue, glyphCount));

            // Build tables
            var tables = new List<Table>();
            tables.Add(MakeTable(TagCff, cffData.ToArray()));
            tables.Add(MakeTable(TagMaxp, BuildMaxp(numGlyphs)));
            tables.Add(MakeTable(TagOS2, BuildOS2(numGlyphs, decriptor)));
            tables.Add(MakeTable(TagHhea, BuildHhea(numberOfHMetrics: 1, decriptor)));
            tables.Add(MakeTable(TagHmtx, BuildHmtx(numGlyphs, numberOfHMetrics: 1)));
            tables.Add(MakeTable(TagPost, BuildPost()));

            // Name table
            var (family, subfamily, psName) = DeriveNames(decriptor);
            tables.Add(MakeTable(TagName, BuildName(family, subfamily, psName)));
            tables.Add(MakeTable(TagCmap, BuildCmapEmpty()));

            // head must be last so we can patch checksumAdjustment after layout
            tables.Add(MakeTable(TagHead, BuildHead(checksumAdjustment: 0)));

            // Sort tables by tag (recommended)
            tables.Sort((a, b) => a.Tag.CompareTo(b.Tag));

            // Layout calculation
            ushort numTables = (ushort)tables.Count;
            ComputeDirParams(numTables, out ushort searchRange, out ushort entrySelector, out ushort rangeShift);

            int offset = 12 + numTables * 16; // header + records
            for (int i = 0; i < tables.Count; i++)
            {
                var t = tables[i];
                offset = Align4(offset);
                t.Offset = offset;
                t.Checksum = CalcTableChecksum(t.Data);
                tables[i] = t;
                offset += Align4(t.Data.Length);
            }

            // Build font once with head.checksumAdjustment=0 to compute total checksum
            var fontBytes = BuildFontBytes(tables, numTables, searchRange, entrySelector, rangeShift);
            uint totalSum = CalcTableChecksum(fontBytes);
            uint adj = unchecked(ChecksumMagic - totalSum);

            // Patch head table data and its checksum
            for (int i = 0; i < tables.Count; i++)
            {
                if (tables[i].Tag == TagHead)
                {
                    var t = tables[i];
                    WriteUInt32BE(t.Data, 8, adj); // checksumAdjustment is at offset 8
                    t.Checksum = CalcTableChecksum(t.Data);
                    tables[i] = t;
                    break;
                }
            }

            return BuildFontBytes(tables, numTables, searchRange, entrySelector, rangeShift);
        }

        private static Table MakeTable(uint tag, byte[] data)
        {
            return new Table { Tag = tag, Data = data, Checksum = 0, Offset = 0 };
        }

        private static byte[] BuildHead(uint checksumAdjustment)
        {
            using (var ms = new MemoryStream(54))
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt32BE(bw, 0x00010000);      // version 1.0
                WriteUInt32BE(bw, 0x00010000);      // fontRevision 1.0
                WriteUInt32BE(bw, checksumAdjustment);
                WriteUInt32BE(bw, HeadMagic);
                WriteUInt16BE(bw, 0x000B);          // flags (baseline/lsb/pointsize)
                WriteUInt16BE(bw, 1000);            // unitsPerEm
                WriteInt64BE(bw, 0);                // created (epoch)
                WriteInt64BE(bw, 0);                // modified (epoch)
                WriteInt16BE(bw, 0);                // xMin
                WriteInt16BE(bw, -200);             // yMin
                WriteInt16BE(bw, 1000);             // xMax
                WriteInt16BE(bw, 800);              // yMax
                WriteUInt16BE(bw, 0);               // macStyle
                WriteUInt16BE(bw, 8);               // lowestRecPPEM
                WriteInt16BE(bw, 2);                // fontDirectionHint
                WriteInt16BE(bw, 0);                // indexToLocFormat (0)
                WriteInt16BE(bw, 0);                // glyphDataFormat
                return ms.ToArray();
            }
        }

        private static byte[] BuildMaxp(ushort numGlyphs)
        {
            using (var ms = new MemoryStream(6))
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt32BE(bw, 0x00005000); // version 0.5 for CFF
                WriteUInt16BE(bw, numGlyphs);
                return ms.ToArray();
            }
        }

        private static byte[] BuildHhea(ushort numberOfHMetrics, PdfFontDescriptor fontDescriptor)
        {
            using (var ms = new MemoryStream(36))
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt32BE(bw, 0x00010000); // version 1.0
                WriteInt16BE(bw, ClampToShort(fontDescriptor.Ascent, 800));         // Ascender
                WriteInt16BE(bw, ClampToShort(fontDescriptor.Descent, -200));      // Descender
                WriteInt16BE(bw, 0);           // LineGap (0 is safer)
                WriteUInt16BE(bw, (ushort)Math.Max(1, (int)Math.Round(fontDescriptor.MaxWidth != 0 ? fontDescriptor.MaxWidth : 1000)));       // advanceWidthMax
                WriteInt16BE(bw, 0);           // minLeftSideBearing
                WriteInt16BE(bw, 0);           // minRightSideBearing
                WriteInt16BE(bw, (short)Math.Max(0, (int)Math.Round((fontDescriptor.FontBBox.Right) - (fontDescriptor.FontBBox.Left))));        // xMaxExtent
                WriteInt16BE(bw, 1);           // caretSlopeRise (1)
                WriteInt16BE(bw, 0);           // caretSlopeRun (0)
                for (int i = 0; i < 5; i++) WriteInt16BE(bw, 0); // reserved
                WriteInt16BE(bw, 0);           // metricDataFormat
                WriteUInt16BE(bw, numberOfHMetrics);
                return ms.ToArray();
            }
        }

        private static byte[] BuildHmtx(ushort numGlyphs, ushort numberOfHMetrics)
        {
            int len = numberOfHMetrics * 4 + Math.Max(0, numGlyphs - numberOfHMetrics) * 2;
            using (var ms = new MemoryStream(len))
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt16BE(bw, 1000); // One metric: advance 1000
                WriteInt16BE(bw, 0);     // lsb 0
                for (int i = 0; i < numGlyphs - numberOfHMetrics; i++) WriteInt16BE(bw, 0);
                return ms.ToArray();
            }
        }

        private static byte[] BuildPost()
        {
            using (var ms = new MemoryStream(32))
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt32BE(bw, 0x00030000); // version 3.0
                WriteUInt32BE(bw, 0); // italicAngle
                WriteInt16BE(bw, 0);  // underlinePosition
                WriteInt16BE(bw, 50); // underlineThickness
                WriteUInt32BE(bw, 0); // isFixedPitch
                WriteUInt32BE(bw, 0); // minMemType42
                WriteUInt32BE(bw, 0); // maxMemType42
                WriteUInt32BE(bw, 0); // minMemType1
                WriteUInt32BE(bw, 0); // maxMemType1
                return ms.ToArray();
            }
        }

        private static byte[] BuildOS2(ushort numGlyphs, PdfFontDescriptor fontDescriptor)
        {
            using (var ms = new MemoryStream(78))
            using (var bw = new BinaryWriter(ms))
            {
                // Derived values
                short xAvgCharWidth = ClampToShort(fontDescriptor.AvgWidth != 0 ? fontDescriptor.AvgWidth : 500, 500);
                ushort usWeightClass = (ushort)(fontDescriptor.FontWeight >= 100 && fontDescriptor.FontWeight <= 900 ? fontDescriptor.FontWeight : ((fontDescriptor.Flags & PdfFontFlags.ForceBold) == PdfFontFlags.ForceBold ? 700 : 400));
                ushort usWidthClass = 5; // Medium
                short sTypoAscender = ClampToShort(fontDescriptor.Ascent != 0 ? fontDescriptor.Ascent : 800, 800);
                short sTypoDescender = ClampToShort(fontDescriptor.Descent != 0 ? fontDescriptor.Descent : -200, -200);
                short sTypoLineGap = 0;
                ushort usWinAscent = (ushort)Math.Max(0, (int)Math.Round(fontDescriptor.FontBBox.Top != 0 ? fontDescriptor.FontBBox.Top : 800));
                ushort usWinDescent = (ushort)Math.Max(0, -(int)Math.Round(fontDescriptor.FontBBox.Bottom != 0 ? fontDescriptor.FontBBox.Bottom : -200));

                // fsSelection
                ushort fsSelection = 0;
                bool italic = ((fontDescriptor.Flags & PdfFontFlags.Italic) == PdfFontFlags.Italic) || Math.Abs(fontDescriptor.ItalicAngle) > 0.1f;
                bool bold = usWeightClass >= 700;
                if (italic) fsSelection |= 0x0001;
                if (bold) fsSelection |= 0x0020;
                if (!italic && !bold) fsSelection |= 0x0002; // REGULAR

                WriteUInt16BE(bw, 0);                  // version 0
                WriteInt16BE(bw, xAvgCharWidth);       // xAvgCharWidth
                WriteUInt16BE(bw, usWeightClass);      // usWeightClass
                WriteUInt16BE(bw, usWidthClass);       // usWidthClass
                WriteInt16BE(bw, 0);                   // fsType
                WriteInt16BE(bw, 2); WriteInt16BE(bw, -1); // ySubscript
                WriteInt16BE(bw, 0); WriteInt16BE(bw, 0);
                WriteInt16BE(bw, 2); WriteInt16BE(bw, 1);  // ySuperscript
                WriteInt16BE(bw, 0); WriteInt16BE(bw, 0);
                WriteInt16BE(bw, 10);                  // yStrikeoutSize
                WriteInt16BE(bw, 250);                 // yStrikeoutPosition
                WriteInt16BE(bw, 0);                   // sFamilyClass
                // Panose (10 bytes)
                var panose = fontDescriptor.Panose;
                for (int i = 0; i < 10; i++) bw.Write((byte)((panose != null && i < panose.Length) ? panose[i] : 0));
                for (int i = 0; i < 4; i++) WriteUInt32BE(bw, 0); // Unicode ranges
                WriteUInt32BE(bw, 0);                  // achVendID
                WriteUInt16BE(bw, fsSelection);        // fsSelection
                WriteUInt16BE(bw, 0);                  // usFirstCharIndex
                WriteUInt16BE(bw, 0xFFFF);             // usLastCharIndex
                WriteInt16BE(bw, sTypoAscender);       // sTypoAscender
                WriteInt16BE(bw, sTypoDescender);      // sTypoDescender
                WriteInt16BE(bw, sTypoLineGap);        // sTypoLineGap
                WriteUInt16BE(bw, usWinAscent);        // usWinAscent
                WriteUInt16BE(bw, usWinDescent);       // usWinDescent
                return ms.ToArray();
            }
        }

        private static byte[] BuildName(string family, string subfamily, string psName)
        {
            var records = new List<(ushort nameId, string value)>
            {
                (1, family), (2, subfamily), (4, $"{family} {subfamily}"), (6, psName), (5, "Version 1.0")
            };
            const ushort platform = 3, encoding = 1, language = 0x0409; // Windows, Unicode BMP, English (US)

            var stringBytes = new List<byte[]>();
            foreach (var r in records) stringBytes.Add(Encoding.BigEndianUnicode.GetBytes(r.value));

            ushort count = (ushort)records.Count;
            int stringOffset = 6 + count * 12;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt16BE(bw, 0);
                WriteUInt16BE(bw, count);
                WriteUInt16BE(bw, (ushort)stringOffset);

                int offset = 0;
                for (int i = 0; i < count; i++)
                {
                    WriteUInt16BE(bw, platform);
                    WriteUInt16BE(bw, encoding);
                    WriteUInt16BE(bw, language);
                    WriteUInt16BE(bw, records[i].nameId);
                    WriteUInt16BE(bw, (ushort)stringBytes[i].Length);
                    WriteUInt16BE(bw, (ushort)offset);
                    offset += stringBytes[i].Length;
                }
                foreach (var s in stringBytes) bw.Write(s, 0, s.Length);
                return ms.ToArray();
            }
        }

        private static byte[] BuildCmapEmpty()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt16BE(bw, 0); // version
                WriteUInt16BE(bw, 1); // numTables
                WriteUInt16BE(bw, 3); // platformID (Windows)
                WriteUInt16BE(bw, 1); // encodingID (Unicode BMP)
                WriteUInt32BE(bw, 12); // offset to subtable

                // Subtable format 4
                WriteUInt16BE(bw, 4); WriteUInt16BE(bw, 16); WriteUInt16BE(bw, 0); // format, length, lang
                WriteUInt16BE(bw, 2); WriteUInt16BE(bw, 2); WriteUInt16BE(bw, 0); WriteUInt16BE(bw, 0); // segCountX2, searchRange, entrySelector, rangeShift
                WriteUInt16BE(bw, 0xFFFF); // endCount
                WriteUInt16BE(bw, 0);      // reservedPad
                WriteUInt16BE(bw, 0xFFFF); // startCount
                WriteUInt16BE(bw, 1);      // idDelta
                WriteUInt16BE(bw, 0);      // idRangeOffset
                return ms.ToArray();
            }
        }

        private static byte[] BuildFontBytes(List<Table> tables, ushort numTables, ushort searchRange, ushort entrySelector, ushort rangeShift)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                WriteUInt32BE(bw, SfntVersion);
                WriteUInt16BE(bw, numTables);
                WriteUInt16BE(bw, searchRange);
                WriteUInt16BE(bw, entrySelector);
                WriteUInt16BE(bw, rangeShift);

                foreach (var t in tables)
                {
                    WriteUInt32BE(bw, t.Tag);
                    WriteUInt32BE(bw, t.Checksum);
                    WriteUInt32BE(bw, (uint)t.Offset);
                    WriteUInt32BE(bw, (uint)t.Data.Length);
                }

                foreach (var t in tables)
                {
                    while (ms.Position < t.Offset) bw.Write((byte)0);
                    bw.Write(t.Data, 0, t.Data.Length);
                    int padded = Align4(t.Data.Length);
                    for (int i = t.Data.Length; i < padded; i++) bw.Write((byte)0);
                }
                return ms.ToArray();
            }
        }

        private static void ComputeDirParams(ushort numTables, out ushort searchRange, out ushort entrySelector, out ushort rangeShift)
        {
            ushort maxPow2 = 1;
            entrySelector = 0;
            while ((maxPow2 << 1) <= numTables)
            {
                maxPow2 <<= 1;
                entrySelector++;
            }
            searchRange = (ushort)(maxPow2 * 16);
            rangeShift = (ushort)(numTables * 16 - searchRange);
        }

        private static int Align4(int n) => (n + 3) & ~3;

        private static uint CalcTableChecksum(byte[] data)
        {
            uint sum = 0;
            int len = Align4(data.Length);
            for (int i = 0; i < len; i += 4)
            {
                uint v = 0;
                if (i < data.Length) v |= (uint)data[i] << 24;
                if (i + 1 < data.Length) v |= (uint)data[i + 1] << 16;
                if (i + 2 < data.Length) v |= (uint)data[i + 2] << 8;
                if (i + 3 < data.Length) v |= (uint)data[i + 3];
                sum += v;
            }
            return sum;
        }

        private static void WriteUInt16BE(BinaryWriter bw, ushort value)
        {
            bw.Write((byte)(value >> 8));
            bw.Write((byte)value);
        }

        private static void WriteInt16BE(BinaryWriter bw, short value) => WriteUInt16BE(bw, (ushort)value);

        private static void WriteUInt32BE(BinaryWriter bw, uint value)
        {
            bw.Write((byte)(value >> 24));
            bw.Write((byte)(value >> 16));
            bw.Write((byte)(value >> 8));
            bw.Write((byte)value);
        }

        private static void WriteInt64BE(BinaryWriter bw, long value)
        {
            WriteUInt32BE(bw, (uint)(value >> 32));
            WriteUInt32BE(bw, (uint)value);
        }

        private static void WriteUInt32BE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static (string family, string subfamily, string psName) DeriveNames(PdfFontDescriptor fd)
        {
            string baseName = fd?.FontName;
            if (string.IsNullOrEmpty(baseName)) baseName = "CFFWrapped";

            // Strip subset prefix ABCDEF+
            int plus = baseName.IndexOf('+');
            if (plus >= 0 && plus < baseName.Length - 1)
            {
                baseName = baseName.Substring(plus + 1);
            }

            string family = fd?.FontFamily;
            if (string.IsNullOrEmpty(family))
            {
                family = baseName;
                string[] suffixes = { "-BoldItalic", "-BoldOblique", "-Bold", "-Italic", "-Oblique" };
                foreach (var s in suffixes)
                {
                    if (family.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    {
                        family = family.Substring(0, family.Length - s.Length);
                        break;
                    }
                }
            }

            bool italic = fd != null && (((fd.Flags & PdfFontFlags.Italic) == PdfFontFlags.Italic) || Math.Abs(fd.ItalicAngle) > 0.1f);
            bool bold = fd != null && (fd.FontWeight >= 700 || (fd.Flags & PdfFontFlags.ForceBold) == PdfFontFlags.ForceBold);

            string subfamily = (bold, italic) switch
            {
                (true, true) => "Bold Italic",
                (true, false) => "Bold",
                (false, true) => "Italic",
                _ => "Regular"
            };

            string psName = baseName.Replace(' ', '-');
            return (family, subfamily, psName);
        }

        private static short ClampToShort(float value, short fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            int v = (int)Math.Round(value);
            if (v > short.MaxValue) return short.MaxValue;
            if (v < short.MinValue) return short.MinValue;
            return (short)v;
        }
    }
}
