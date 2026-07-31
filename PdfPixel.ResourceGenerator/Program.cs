using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.ResourceGenerator.Cmaps;
using PdfPixel.ResourceGenerator.Encodings;
using PdfPixel.ResourceGenerator.Metrics;

namespace PdfPixel.ResourceGenerator;

internal static class Program
{
    private const string CmapSourceUrl = "https://github.com/adobe-type-tools/cmap-resources/archive/refs/tags/20231115.zip";
    private const string MappingResourcesUrl = "https://github.com/adobe-type-tools/mapping-resources-pdf/archive/refs/tags/20230118.zip";
    private const string AglUrl = "https://raw.githubusercontent.com/adobe-type-tools/agl-aglfn/master/glyphlist.txt";
    private const string ZapfDingbatsUrl = "https://raw.githubusercontent.com/adobe-type-tools/agl-aglfn/master/zapfdingbats.txt";

    private const string CmapWorkDirectory = "CMaps";
    private const string MappingResourcesWorkDirectory = "MappingResources";
    private const string AglWorkDirectory = "AGL";

    private const string CmapOutputDirectory = "CMaps";
    private const string CidToUnicodeOutputDirectory = "CidToUnicode";
    private const string Cid2CodeOutputDirectory = "Cid2Code";
    private const string GlyphNamesOutputDirectory = "GlyphNames";
    private const string Standard14WidthsOutputDirectory = "Standard14Widths";
    private const string PostGlyphOrderOutputDirectory = "PostGlyphOrder";
    private const string StandardFontGlyphMapOutputDirectory = "StandardFontGlyphMap";

    private static async Task<int> Main()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        foreach (string dir in new[]
        {
            CmapOutputDirectory,
            CidToUnicodeOutputDirectory,
            Cid2CodeOutputDirectory,
            GlyphNamesOutputDirectory,
            Standard14WidthsOutputDirectory,
            PostGlyphOrderOutputDirectory,
            StandardFontGlyphMapOutputDirectory
        })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        string cmapWorkDirectory = Path.Combine(AppContext.BaseDirectory, "temp", CmapWorkDirectory);
        if (Directory.Exists(cmapWorkDirectory))
        {
            Directory.Delete(cmapWorkDirectory, recursive: true);
        }

        string sourceRoot = await CmapSourceDownloader.DownloadAndExtractAsync(CmapSourceUrl, cmapWorkDirectory).ConfigureAwait(false);

        IReadOnlyList<PdfCMap> cmaps = CmapSourceParser.ParseAll(sourceRoot, loggerFactory);

        Console.WriteLine($"Compressing CMaps to {CmapOutputDirectory} ...");
        CmapCompressor.CompressCmaps(cmaps, CmapOutputDirectory);

        Console.WriteLine($"Generating CID-to-Unicode (cid2code) to {Cid2CodeOutputDirectory} ...");
        CidToUnicodeGenerator.GenerateAll(sourceRoot, Cid2CodeOutputDirectory);

        string mappingResourcesWorkDirectory = Path.Combine(AppContext.BaseDirectory, "temp", MappingResourcesWorkDirectory);
        if (Directory.Exists(mappingResourcesWorkDirectory))
        {
            Directory.Delete(mappingResourcesWorkDirectory, recursive: true);
        }

        string mappingResourcesRoot = await CmapSourceDownloader.DownloadAndExtractAsync(MappingResourcesUrl, mappingResourcesWorkDirectory).ConfigureAwait(false);

        Console.WriteLine($"Generating CID-to-Unicode (pdf2unicode) to {CidToUnicodeOutputDirectory} ...");
        CidToUnicodeFromPdfCMapGenerator.GenerateAll(mappingResourcesRoot, CidToUnicodeOutputDirectory, loggerFactory);

        string aglWorkDirectory = Path.Combine(AppContext.BaseDirectory, "temp", AglWorkDirectory);
        if (Directory.Exists(aglWorkDirectory))
        {
            Directory.Delete(aglWorkDirectory, recursive: true);
        }

        Console.WriteLine($"Generating glyph names to {GlyphNamesOutputDirectory} ...");
        await AglGenerator.GenerateAsync(AglUrl, ZapfDingbatsUrl, aglWorkDirectory, GlyphNamesOutputDirectory).ConfigureAwait(false);

        string aglOverridesPath = Path.Combine(AppContext.BaseDirectory, "AglOverrides.txt");
        AglGenerator.GenerateFromFile(aglOverridesPath, GlyphNamesOutputDirectory);

        Console.WriteLine($"Generating Standard 14 widths to {Standard14WidthsOutputDirectory} ...");
        Standard14WidthGenerator.GenerateAll(Standard14WidthsOutputDirectory);

        Console.WriteLine($"Generating post table standard glyph order to {PostGlyphOrderOutputDirectory} ...");
        string postGlyphOrderSourcePath = Path.Combine(AppContext.BaseDirectory, "Encodings", "PostGlyphOrder.txt");
        PostGlyphOrderGenerator.Generate(postGlyphOrderSourcePath, PostGlyphOrderOutputDirectory);

        Console.WriteLine($"Generating standard font glyph maps to {StandardFontGlyphMapOutputDirectory} ...");
        string standardFontGlyphMapSourceDirectory = Path.Combine(AppContext.BaseDirectory, "Encodings");
        StandardFontGlyphMapGenerator.GenerateAll(standardFontGlyphMapSourceDirectory, StandardFontGlyphMapOutputDirectory);

        Console.WriteLine("Done.");
        return 0;
    }

}
