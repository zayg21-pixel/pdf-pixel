using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Resources;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Manages CMap caches and provides CMap loading for a PDF document.
/// </summary>
internal class CMapCache
{
    private readonly ILogger _logger;
    private readonly PdfDocument _document;

    /// <summary>
    /// Global cache for parsed CMaps by name.
    /// </summary>
    private static readonly Dictionary<PdfString, PdfCMap> GlobalCMaps = new Dictionary<PdfString, PdfCMap>();

    /// <summary>
    /// Per-document cache for CMaps loaded from streams.
    /// </summary>
    internal Dictionary<PdfReference, PdfCMap> CMapStreams { get; } = new Dictionary<PdfReference, PdfCMap>();

    public CMapCache(PdfDocument document, ILogger logger)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a CMap by name, loading and caching it if necessary.
    /// </summary>
    /// <param name="name">The CMap name.</param>
    /// <returns>The loaded <see cref="PdfCMap"/> or null if not found.</returns>
    public PdfCMap GetCmap(PdfString name)
    {
        if (GlobalCMaps.TryGetValue(name, out var existing))
        {
            return existing;
        }

        try
        {
            var cmapBytes = PdfResourceLoader.GetResource($"CMaps.{name}.bin");
            var cmap = PdfCmapBinary.ParseCMapBinary(cmapBytes, GetCmap);

            if (cmap != null)
            {
                GlobalCMaps[name] = cmap;
            }

            return cmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load CMap '{CMapName}'", name.ToString());
            return null;
        }
    }
}
