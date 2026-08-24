using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.ResourceGenerator.Cmaps;
using PdfPixel.ResourceGenerator.Encodings;

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
    private const string GlyphNamesOutputDirectory = "GlyphNames";
    private const string PostGlyphOrderOutputDirectory = "PostGlyphOrder";

    /// <summary>
    /// Every section that can be generated, named by the directory it writes to.
    /// </summary>
    private static readonly string[] SectionNames =
    [
        CmapOutputDirectory,
        CidToUnicodeOutputDirectory,
        GlyphNamesOutputDirectory,
        PostGlyphOrderOutputDirectory
    ];

    /// <summary>
    /// Generates the sections <paramref name="args"/> names, or every section when it names none.
    /// Returns 1 without generating anything when it names a section that does not exist.
    /// </summary>
    private static async Task<int> Main(string[] args)
    {
        HashSet<string> sections = new(args, StringComparer.OrdinalIgnoreCase);

        foreach (string section in sections)
        {
            if (!SectionNames.Contains(section, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Unknown section '{section}'.");
                Console.Error.WriteLine($"Usage: PdfPixel.ResourceGenerator [section ...], where a section is one of: {string.Join(", ", SectionNames)}.");
                Console.Error.WriteLine("Naming no section generates all of them.");
                return 1;
            }
        }

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        foreach (string section in SectionNames)
        {
            string sectionDirectory = GetOutputDirectory(section);

            if (IsSelected(sections, section) && Directory.Exists(sectionDirectory))
            {
                Directory.Delete(sectionDirectory, recursive: true);
            }
        }

        if (IsSelected(sections, CmapOutputDirectory))
        {
            string cmapWorkDirectory = Path.Combine(AppContext.BaseDirectory, "temp", CmapWorkDirectory);
            if (Directory.Exists(cmapWorkDirectory))
            {
                Directory.Delete(cmapWorkDirectory, recursive: true);
            }

            string sourceRoot = await CmapSourceDownloader.DownloadAndExtractAsync(CmapSourceUrl, cmapWorkDirectory).ConfigureAwait(false);

            IReadOnlyList<PdfCMap> cmaps = CmapSourceParser.ParseAll(sourceRoot, loggerFactory);

            Console.WriteLine($"Compressing CMaps to {CmapOutputDirectory} ...");
            CmapCompressor.CompressCmaps(cmaps, GetOutputDirectory(CmapOutputDirectory));
        }

        if (IsSelected(sections, CidToUnicodeOutputDirectory))
        {
            string mappingResourcesWorkDirectory = Path.Combine(AppContext.BaseDirectory, "temp", MappingResourcesWorkDirectory);
            if (Directory.Exists(mappingResourcesWorkDirectory))
            {
                Directory.Delete(mappingResourcesWorkDirectory, recursive: true);
            }

            string mappingResourcesRoot = await CmapSourceDownloader.DownloadAndExtractAsync(MappingResourcesUrl, mappingResourcesWorkDirectory).ConfigureAwait(false);

            Console.WriteLine($"Generating CID-to-Unicode (pdf2unicode) to {CidToUnicodeOutputDirectory} ...");
            CidToUnicodeFromPdfCMapGenerator.GenerateAll(mappingResourcesRoot, GetOutputDirectory(CidToUnicodeOutputDirectory), loggerFactory);
        }

        if (IsSelected(sections, GlyphNamesOutputDirectory))
        {
            string aglWorkDirectory = Path.Combine(AppContext.BaseDirectory, "temp", AglWorkDirectory);
            if (Directory.Exists(aglWorkDirectory))
            {
                Directory.Delete(aglWorkDirectory, recursive: true);
            }

            Console.WriteLine($"Generating glyph names to {GlyphNamesOutputDirectory} ...");
            await AglGenerator.GenerateAsync(AglUrl, ZapfDingbatsUrl, aglWorkDirectory, GetOutputDirectory(GlyphNamesOutputDirectory)).ConfigureAwait(false);

            string aglOverridesPath = Path.Combine(AppContext.BaseDirectory, "AglOverrides.txt");
            AglGenerator.GenerateFromFile(aglOverridesPath, GetOutputDirectory(GlyphNamesOutputDirectory));
        }

        if (IsSelected(sections, PostGlyphOrderOutputDirectory))
        {
            Console.WriteLine($"Generating post table standard glyph order to {PostGlyphOrderOutputDirectory} ...");
            string postGlyphOrderSourcePath = Path.Combine(AppContext.BaseDirectory, "Encodings", "PostGlyphOrder.txt");
            PostGlyphOrderGenerator.Generate(postGlyphOrderSourcePath, GetOutputDirectory(PostGlyphOrderOutputDirectory));
        }

        Console.WriteLine("Done.");
        return 0;
    }

    /// <summary>
    /// Reports whether <paramref name="section"/> is to be generated: every section when
    /// <paramref name="sections"/> is empty, otherwise only the ones it names.
    /// </summary>
    private static bool IsSelected(HashSet<string> sections, string section) => sections.Count == 0 || sections.Contains(section);

    /// <summary>
    /// The directory <paramref name="section"/> is written to, under the base directory this process
    /// was loaded from. Every section is emptied before it is written, so the path it resolves to must
    /// not depend on the current working directory.
    /// </summary>
    private static string GetOutputDirectory(string section) => Path.Combine(AppContext.BaseDirectory, section);
}
