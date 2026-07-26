using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Wraps a repacked CFF (Type 1C) typeface in a minimal OpenType (OTTO) container, synthesizing
/// mandatory tables with placeholder metrics.
/// </summary>
public static class CffOpenTypeWrapper
{
    private const uint OttoVersion = 0x4F54544F; // 'OTTO'

    private const ushort NameIdFontFamily = 1;
    private const ushort NameIdFontSubfamily = 2;
    private const ushort NameIdUniqueIdentifier = 3;
    private const ushort NameIdFullFontName = 4;
    private const ushort NameIdVersionString = 5;
    private const ushort NameIdPostScriptName = 6;
    private const ushort NameLanguageEnUs = 0x0409;

    private const short DefaultAscent = 800;
    private const short DefaultDescent = -200;
    private const short DefaultUnitsPerEm = 1000;
    private const short DefaultStrikeoutSize = 10;
    private const short DefaultStrikeoutPosition = 250;
    private const ushort DefaultLowestRecPpem = 17;
    private const short HeadXMax = 0x0FFF;
    private const ushort DefaultWeightIfUnknown = 400;
    private const short DefaultAvgWidth = 500;
    private const string DefaultFamilyBaseName = "CFFWrapped";
    private const string DefaultVersionString = "Version 1.0";

    private static readonly SfntFontProcessor FontProcessor = new(NullLoggerFactory.Instance);

    /// <summary>
    /// Wraps the typeface in a minimal OpenType font byte array with placeholder metrics, repacking
    /// the CFF data via <see cref="SfntFontProcessor"/>. Returns null if the typeface is empty.
    /// </summary>
    public static byte[]? Wrap(CffTypeface? typeface)
    {
        if (typeface == null || typeface.Fonts.Length == 0)
        {
            return null;
        }

        CffFont font = typeface.Fonts[0];

        PdfFontMatrix fontMatrix = CffTopDict.CombineFontMatrix(font.Dict.TopDict, null);

        var gidWidths = new float[font.Characters.Length];
        for (int glyphIndex = 0; glyphIndex < font.Characters.Length; glyphIndex++)
        {
            CffCharacter character = font.Characters[glyphIndex];
            gidWidths[glyphIndex] = character.Width * character.GetEffectiveMatrix(font).ScaleX;
        }

        var numGlyphs = (ushort)Math.Max(1, Math.Min(ushort.MaxValue, font.Characters.Length));
        var unitsPerEm = (ushort)Math.Max(1, Math.Min(ushort.MaxValue, Math.Round(fontMatrix.UnitsPerEm)));
        float unitsPerEmScale = unitsPerEm / (float)DefaultUnitsPerEm;

        SfntFont sfntFont = new()
        {
            Version = OttoVersion,
            CffTypeface = typeface,
            Head = BuildHead(unitsPerEm, unitsPerEmScale),
            Hhea = BuildHhea(numGlyphs, unitsPerEmScale),
            Maxp = new SfntMaxp { Version = 0.5f, NumGlyphs = numGlyphs },
            Hmtx = BuildHmtx(numGlyphs, gidWidths, unitsPerEm),
            Os2 = BuildOs2(unitsPerEm, unitsPerEmScale),
            Name = BuildName()
        };

        return FontProcessor.Write(sfntFont, ReadOnlyFontStream.Empty);
    }

    private static SfntHead BuildHead(ushort unitsPerEm, float unitsPerEmScale)
    {
        return new()
        {
            Version = 1.0f,
            FontRevision = 1.0f,
            Flags = 0,
            UnitsPerEm = unitsPerEm,
            Created = 0,
            Modified = 0,
            XMin = 0,
            YMin = (short)Math.Round(DefaultDescent * unitsPerEmScale),
            XMax = HeadXMax,
            YMax = (short)Math.Round(DefaultAscent * unitsPerEmScale),
            MacStyle = 0,
            LowestRecPpem = DefaultLowestRecPpem,
            FontDirectionHint = 0,
            IndexToLocFormat = 0,
            GlyphDataFormat = 0
        };
    }

    private static SfntHhea BuildHhea(ushort numGlyphs, float unitsPerEmScale)
    {
        return new()
        {
            Version = 1.0f,
            Ascender = (short)Math.Round(DefaultAscent * unitsPerEmScale),
            Descender = (short)Math.Round(DefaultDescent * unitsPerEmScale),
            LineGap = 0,
            AdvanceWidthMax = ushort.MaxValue,
            MinLeftSideBearing = 0,
            MinRightSideBearing = 0,
            XMaxExtent = 0,
            CaretSlopeRise = 0,
            CaretSlopeRun = 0,
            CaretOffset = 0,
            MetricDataFormat = 0,
            NumberOfHMetrics = numGlyphs // one real entry per glyph, no compression.
        };
    }

    private static SfntHmtx BuildHmtx(ushort numGlyphs, float[] gidWidths, ushort unitsPerEm)
    {
        var metrics = new SfntHorizontalMetric[numGlyphs];
        for (int glyphId = 0; glyphId < numGlyphs; glyphId++)
        {
            float glyphSpaceWidth = (glyphId < gidWidths.Length) ? gidWidths[glyphId] : 0;
            var advanceWidth = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, Math.Ceiling(glyphSpaceWidth * unitsPerEm)));
            metrics[glyphId] = new SfntHorizontalMetric(advanceWidth, leftSideBearing: 0);
        }

        return new SfntHmtx { Metrics = metrics };
    }

    private static SfntOs2 BuildOs2(ushort unitsPerEm, float unitsPerEmScale)
    {
        var typoAscender = (short)Math.Round(DefaultAscent * unitsPerEmScale);
        var typoDescender = (short)Math.Round(DefaultDescent * unitsPerEmScale);

        return new SfntOs2
        {
            Version = 4, // Windows rejects version 0.
            XAvgCharWidth = (short)Math.Round(DefaultAvgWidth * unitsPerEmScale),
            UsWeightClass = DefaultWeightIfUnknown,
            UsWidthClass = 5, // medium width
            FsType = 0,
            YSubscriptXSize = 2,
            YSubscriptYSize = -1,
            YSubscriptXOffset = 0,
            YSubscriptYOffset = 0,
            YSuperscriptXSize = 2,
            YSuperscriptYSize = 1,
            YSuperscriptXOffset = 0,
            YSuperscriptYOffset = 0,
            YStrikeoutSize = DefaultStrikeoutSize,
            YStrikeoutPosition = DefaultStrikeoutPosition,
            SFamilyClass = 0,
            Panose = new byte[10],
            UlUnicodeRange1 = 0,
            UlUnicodeRange2 = 0,
            UlUnicodeRange3 = 0,
            UlUnicodeRange4 = 0,
            AchVendId = new SfntTableTag(0),
            FsSelection = 0x0002, // REGULAR
            UsFirstCharIndex = 0,
            UsLastCharIndex = 0xFFFF, // placeholder span
            STypoAscender = typoAscender,
            STypoDescender = typoDescender,
            STypoLineGap = 0,
            UsWinAscent = (ushort)Math.Max(0, (int)typoAscender),
            UsWinDescent = (ushort)Math.Max(0, -(int)typoDescender),
            UlCodePageRange1 = 0x00000001, // Latin 1 (CP1252)
            UlCodePageRange2 = 0,
            SxHeight = (short)Math.Round(0.5f * unitsPerEm),
            SCapHeight = (short)Math.Round(0.7f * unitsPerEm),
            UsDefaultChar = 0,
            UsBreakChar = 0x0020, // space
            UsMaxContext = 1
        };
    }

    private static SfntName BuildName()
    {
        const string family = DefaultFamilyBaseName;
        const string subfamily = "Regular";
        const string postScriptName = DefaultFamilyBaseName;

        List<SfntNameRecord> records =
        [
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, NameIdFontFamily, family),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, NameIdFontSubfamily, subfamily),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, NameIdUniqueIdentifier, $"{DefaultVersionString};{postScriptName}"),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, NameIdFullFontName, $"{family} {subfamily}"),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, NameIdVersionString, DefaultVersionString),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, NameIdPostScriptName, postScriptName)
        ];

        return new SfntName { Records = records };
    }
}
