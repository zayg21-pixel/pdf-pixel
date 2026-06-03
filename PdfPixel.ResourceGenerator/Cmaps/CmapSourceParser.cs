using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;

namespace PdfPixel.ResourceGenerator.Cmaps;

/// <summary>
/// Scans an extracted Adobe CMap resources directory and parses all CMap files.
/// </summary>
internal static class CmapSourceParser
{
    /// <summary>
    /// Scans all immediate subdirectories of <paramref name="sourceRoot"/> for CMap files,
    /// parses each one, and returns the full collection.
    /// Dependencies referenced via <c>usecmap</c> are resolved on demand.
    /// </summary>
    public static IReadOnlyList<PdfCMap> ParseAll(string sourceRoot, ILoggerFactory loggerFactory)
    {
        List<string> allFiles = [];
        Dictionary<string, string> resolver = [];

        CollectFiles(sourceRoot, allFiles, resolver);

        Console.WriteLine($"Found {allFiles.Count} CMap files.");

        Dictionary<string, PdfCMap> resolverCache = new(StringComparer.Ordinal);
        List<PdfCMap> result = [];

        foreach (string filePath in allFiles)
        {
            PdfCMap? cmap = ParseFile(filePath, resolver, resolverCache, loggerFactory);
            if (cmap != null && !cmap.Name.IsEmpty)
            {
                result.Add(cmap);
            }
        }

        Console.WriteLine($"Parsed {result.Count} CMaps.");
        return result;
    }

    private static void CollectFiles(string sourceRoot, List<string> allFiles, Dictionary<string, string> resolver)
    {
        foreach (string cmapDirectory in Directory.GetDirectories(sourceRoot, "CMap", SearchOption.AllDirectories))
        {
            foreach (string filePath in Directory.GetFiles(cmapDirectory))
            {
                if (!string.IsNullOrEmpty(Path.GetExtension(filePath)))
                {
                    continue;
                }

                allFiles.Add(filePath);

                string name = Path.GetFileName(filePath);
                resolver.TryAdd(name, filePath);
            }
        }
    }

    private static PdfCMap? ParseFile(string filePath, Dictionary<string, string> resolver, Dictionary<string, PdfCMap> resolverCache, ILoggerFactory loggerFactory)
    {
        ReadOnlyMemory<byte> bytes = File.ReadAllBytes(filePath);
        PdfCMap? cmap = PdfCMapParser.ParseCMap(
            bytes,
            loggerFactory,
            dependency => ResolveUseCmap(dependency.ToString(), resolver, resolverCache, loggerFactory)
        );

        if (cmap == null || cmap.Name.IsEmpty)
        {
            Console.WriteLine($"    -> skipped (null or empty name)");
        }
        else
        {
            Console.WriteLine($"    -> parsed as: {cmap.Name}");
        }

        return cmap;
    }

    private static PdfCMap? ResolveUseCmap(string name, Dictionary<string, string> resolver, Dictionary<string, PdfCMap> resolverCache, ILoggerFactory loggerFactory)
    {
        if (resolverCache.TryGetValue(name, out PdfCMap? cached))
        {
            return cached;
        }

        if (!resolver.TryGetValue(name, out string? filePath))
        {
            return null;
        }

        PdfCMap? cmap = ParseFile(filePath, resolver, resolverCache, loggerFactory);
        if (cmap != null)
        {
            resolverCache[name] = cmap;
        }

        return cmap;
    }
}
