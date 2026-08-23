using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.ResourceGenerator.Fonts;

/// <summary>
/// Builds one substitute font file per Standard 14 font. The twelve text fonts are subsets of the
/// matching Croscore font, carrying the advance widths of the Adobe AFM they stand in for; Symbol and
/// ZapfDingbats are PDFium's CFF programs wrapped whole in an OpenType container.
/// </summary>
internal static class Standard14FontGenerator
{
    private const ushort WindowsPlatformId = 3;
    private const ushort UnicodeBmpEncodingId = 1;
    private const ushort SymbolEncodingId = 0;
    private const ushort NameLanguageEnUs = 0x0409;

    /// <summary>
    /// The 0xF000 page the PDF specification's symbolic TrueType convention (9.6.6.4) addresses a
    /// symbol font's glyphs through.
    /// </summary>
    private const int SymbolCodePage = 0xF000;

    private const float AfmUnitsPerEm = 1000f;

    private static readonly (string Standard14Name, string SourceFileName)[] TextFonts =
    [
        ("Courier", "Cousine-Regular.ttf"),
        ("Courier-Bold", "Cousine-Bold.ttf"),
        ("Courier-Oblique", "Cousine-Italic.ttf"),
        ("Courier-BoldOblique", "Cousine-BoldItalic.ttf"),
        ("Helvetica", "Arimo-Regular.ttf"),
        ("Helvetica-Bold", "Arimo-Bold.ttf"),
        ("Helvetica-Oblique", "Arimo-Italic.ttf"),
        ("Helvetica-BoldOblique", "Arimo-BoldItalic.ttf"),
        ("Times-Roman", "Tinos-Regular.ttf"),
        ("Times-Bold", "Tinos-Bold.ttf"),
        ("Times-Italic", "Tinos-Italic.ttf"),
        ("Times-BoldItalic", "Tinos-BoldItalic.ttf")
    ];

    private static readonly (string Standard14Name, string SourceFileName)[] SymbolicFonts =
    [
        ("Symbol", "FoxitSymbol.cff"),
        ("ZapfDingbats", "FoxitDingbats.cff")
    ];

    /// <summary>
    /// Writes all fourteen font files into <paramref name="outputDirectory"/>, named for the Standard 14
    /// font each stands in for.
    /// </summary>
    public static void GenerateAll(
        string croscoreDirectory,
        string foxitDirectory,
        string metricsDirectory,
        string outputDirectory,
        ILoggerFactory loggerFactory)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach ((string standard14Name, string sourceFileName) in TextFonts)
        {
            GenerateTextFont(
                standard14Name,
                Path.Combine(croscoreDirectory, sourceFileName),
                Path.Combine(metricsDirectory, standard14Name + ".afm"),
                Path.Combine(outputDirectory, standard14Name + ".ttf"),
                loggerFactory);
        }

        foreach ((string standard14Name, string sourceFileName) in SymbolicFonts)
        {
            GenerateSymbolicFont(
                standard14Name,
                Path.Combine(foxitDirectory, sourceFileName),
                Path.Combine(metricsDirectory, standard14Name + ".afm"),
                Path.Combine(outputDirectory, standard14Name + ".otf"),
                loggerFactory);
        }
    }

    /// <summary>
    /// Subsets the Croscore font at <paramref name="sourcePath"/> to the glyphs the AFM at
    /// <paramref name="afmPath"/> names, in AFM order behind a leading notdef, replacing each one's
    /// advance width with the AFM's scaled into the source font's em square.
    /// </summary>
    private static void GenerateTextFont(
        string standard14Name,
        string sourcePath,
        string afmPath,
        string outputPath,
        ILoggerFactory loggerFactory)
    {
        IReadOnlyList<AfmCharacterMetric> characters = AfmParser.Parse(afmPath);

        SfntPdfTypefaceParameters parameters = new() { RepackTypeface = false };
        using SfntPdfTypeface typeface = new(new MemoryStream(File.ReadAllBytes(sourcePath)), loggerFactory, parameters);

        SfntFont font = typeface.SfntFont;
        if (font.Head == null || font.Hmtx == null)
        {
            throw new InvalidOperationException($"'{sourcePath}' states no 'head' or 'hmtx' table.");
        }

        float widthScale = font.Head.UnitsPerEm / AfmUnitsPerEm;

        List<ushort> glyphOrder = [0];
        Dictionary<int, ushort> codeToGid = [];
        SfntHorizontalMetric[] metrics = [.. font.Hmtx.Metrics];

        foreach (AfmCharacterMetric character in characters)
        {
            PdfFontString glyphName = PdfFontString.FromString(character.Name);
            string? unicode = GetUnicode(glyphName);

            ushort? sourceGid = ResolveGid(typeface, glyphName, unicode);
            if (sourceGid == null)
            {
                Console.WriteLine($"    '{character.Name}' is absent from {Path.GetFileName(sourcePath)}.");
                continue;
            }

            var outputGid = (ushort)glyphOrder.Count;
            glyphOrder.Add(sourceGid.Value);

            metrics[sourceGid.Value] = new SfntHorizontalMetric(
                (ushort)Math.Round(character.Width * widthScale),
                metrics[sourceGid.Value].LeftSideBearing);

            AddUnicodeMapping(codeToGid, unicode, outputGid);
        }

        font.Hmtx.Metrics = metrics;

        parameters.RepackTypeface = true;
        parameters.Repack = new SfntPdfTypefaceRepackParameters
        {
            GlyphOrder = glyphOrder,
            CodeToGid = codeToGid,
            CmapPlatformId = WindowsPlatformId,
            CmapEncodingId = UnicodeBmpEncodingId
        };

        long length = WriteFontStream(typeface, outputPath);

        Console.WriteLine($"  {standard14Name}: {glyphOrder.Count} glyphs, {codeToGid.Count} codes, {length:N0} bytes");
    }

    /// <summary>
    /// Resolves an AFM glyph to its source glyph id through the font's own "post" name, then through
    /// the Unicode the Adobe Glyph List resolves that name to. Returns null when neither addresses a
    /// glyph, which a Croscore font naming its glyphs "uniXXXX" makes the ordinary case for the first.
    /// </summary>
    private static ushort? ResolveGid(SfntPdfTypeface typeface, in PdfFontString glyphName, string? unicode)
    {
        IReadOnlyDictionary<PdfFontString, ushort>? nameToGid = typeface.SfntFont.Post?.NameToGid;
        if (nameToGid != null && nameToGid.TryGetValue(glyphName, out ushort postGid) && postGid != 0)
        {
            return postGid;
        }

        if (unicode == null)
        {
            return null;
        }

        ushort? cmapGid = typeface.GetGid(unicode);
        if (cmapGid is null or 0)
        {
            return null;
        }

        return cmapGid;
    }

    /// <summary>
    /// The Unicode the Adobe Glyph List resolves <paramref name="glyphName"/> to, or null when it
    /// resolves to none.
    /// </summary>
    private static string? GetUnicode(in PdfFontString glyphName)
    {
        AdobeGlyphList.TryGetUnicode(PdfFontEncoding.StandardEncoding, glyphName, out string? unicode);
        return unicode;
    }

    /// <summary>
    /// Writes the typeface's repacked bytes to <paramref name="outputPath"/>, returning how many.
    /// </summary>
    private static long WriteFontStream(SfntPdfTypeface typeface, string outputPath)
    {
        using Stream repacked = typeface.GetFontStream();
        using FileStream output = File.Create(outputPath);

        repacked.CopyTo(output);

        return output.Length;
    }

    /// <summary>
    /// Wraps the bare CFF program at <paramref name="sourcePath"/> in an OpenType container, keeping
    /// every glyph, and maps the AFM's built-in character codes to them through a Windows Symbol "cmap".
    /// </summary>
    private static void GenerateSymbolicFont(
        string standard14Name,
        string sourcePath,
        string afmPath,
        string outputPath,
        ILoggerFactory loggerFactory)
    {
        IReadOnlyList<AfmCharacterMetric> characters = AfmParser.Parse(afmPath);

        CffTypefaceReader cffReader = new(loggerFactory);
        CffTypeface? cffTypeface = cffReader.Read(File.ReadAllBytes(sourcePath));
        if (cffTypeface == null || cffTypeface.Fonts.Length == 0)
        {
            throw new InvalidOperationException($"'{sourcePath}' holds no readable CFF font.");
        }

        CffFont cffFont = cffTypeface.Fonts[0];

        IReadOnlyDictionary<PdfFontString, ushort>? nameToGid = cffFont.NameToGid;
        if (nameToGid == null)
        {
            throw new InvalidOperationException($"'{sourcePath}' carries no CFF charset to map names through.");
        }

        byte[]? wrappedBytes = CffOpenTypeWrapper.Wrap(cffTypeface);
        if (wrappedBytes == null)
        {
            throw new InvalidOperationException($"'{sourcePath}' wrapped to no OpenType font.");
        }

        Dictionary<int, ushort> codeToGid = [];
        foreach (AfmCharacterMetric character in characters)
        {
            if (character.Code < 0)
            {
                continue;
            }

            if (!nameToGid.TryGetValue(PdfFontString.FromString(character.Name), out ushort gid))
            {
                Console.WriteLine($"    '{character.Name}' is absent from {Path.GetFileName(sourcePath)}.");
                continue;
            }

            codeToGid[SymbolCodePage | character.Code] = gid;
        }

        SfntFontProcessor processor = new(loggerFactory);
        using ReadOnlyFontStream fontStream = ReadOnlyFontStream.Create(new MemoryStream(wrappedBytes), leaveOpen: false);

        SfntFont? font = processor.Read(fontStream);
        if (font == null)
        {
            throw new InvalidOperationException($"'{sourcePath}' wrapped to an unreadable OpenType font.");
        }

        font.Name = BuildName(cffFont.Name.ToString());

        SfntPdfTypefaceRepackParameters repackParameters = new()
        {
            CodeToGid = codeToGid,
            CmapPlatformId = WindowsPlatformId,
            CmapEncodingId = SymbolEncodingId
        };

        byte[] fontBytes = processor.Write(font, fontStream, repackParameters);
        File.WriteAllBytes(outputPath, fontBytes);

        Console.WriteLine($"  {standard14Name}: {cffFont.Characters.Length} glyphs, {codeToGid.Count} codes, {fontBytes.Length:N0} bytes");
    }

    /// <summary>
    /// Maps <paramref name="unicode"/> to <paramref name="gid"/>, when it is a single code already
    /// unclaimed by an earlier glyph.
    /// </summary>
    private static void AddUnicodeMapping(Dictionary<int, ushort> codeToGid, string? unicode, ushort gid)
    {
        if (unicode == null || unicode.Length != 1)
        {
            return;
        }

        int code = unicode[0];
        if (codeToGid.ContainsKey(code))
        {
            return;
        }

        codeToGid[code] = gid;
    }

    /// <summary>
    /// Builds a "name" table naming the font <paramref name="family"/>, in the Windows Unicode records
    /// a rasterizer reads.
    /// </summary>
    private static SfntName BuildName(string family)
    {
        List<SfntNameRecord> records =
        [
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, 1, family),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, 2, "Regular"),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, 4, family),
            SfntNameRecord.CreateWindowsUnicode(NameLanguageEnUs, 6, family)
        ];

        return new SfntName { Records = records };
    }
}
