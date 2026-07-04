using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Collects images during the Initialize phase and packs small ones into atlas pages
/// grouped by pixel format. Large images are stored standalone. Each atlas page freezes
/// independently — either when full or on first retrieval.
/// </summary>
internal sealed class PdfCommandImageCache : IDisposable
{
    private const int MaxCachedImageSize = 256; // TODO: [MEDIUM] wire constants to parsing parameters
    private const int MaxPageSize = 512;

    private readonly Dictionary<Guid, SKImage> _largeImages = [];
    private readonly List<AtlasPage> _pages = [];

    /// <summary>
    /// Stores an image in the cache.
    /// </summary>
    public void CacheImage(ref readonly Guid id, SKImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (_largeImages.ContainsKey(id) || FindPage(in id) != null)
        {
            throw new InvalidOperationException($"Image with id {id} is already cached.");
        }

        if (image.Width > MaxCachedImageSize || image.Height > MaxCachedImageSize)
        {
            _largeImages[id] = image;
            return;
        }

        AtlasKey key = new(image.ColorType, image.AlphaType);
        AtlasPage? activePage = FindActivePage(in key);

        if (activePage != null && activePage.TryAdd(in id, image))
        {
            return;
        }

        activePage?.Freeze();

        AtlasPage newPage = new(in key, MaxPageSize);
        _pages.Add(newPage);

        if (!newPage.TryAdd(in id, image))
        {
            throw new InvalidOperationException($"Image {image.Width}x{image.Height} does not fit in a new atlas page.");
        }
    }

    /// <summary>
    /// Retrieves a cached image by id. For small images, freezes the containing atlas page
    /// if it is still mutable.
    /// </summary>
    public CachedImageResult GetImage(ref readonly Guid id)
    {
        if (_largeImages.TryGetValue(id, out SKImage? largeImage))
        {
            return new CachedImageResult(largeImage, SKRectI.Empty, isAtlased: false);
        }

        AtlasPage? page = FindPage(in id);
        if (page != null)
        {
            return page.GetImage(in id);
        }

        throw new KeyNotFoundException($"Image with id {id} was not cached.");
    }

    /// <summary>
    /// Removes a cached image by id. For large images, disposes immediately. For small images
    /// in a frozen page, decrements the live count; when all segments are removed the page
    /// disposes itself.
    /// </summary>
    public void Remove(ref readonly Guid id)
    {
        if (_largeImages.TryGetValue(id, out SKImage? largeImage))
        {
            largeImage.Dispose();
            _largeImages.Remove(id);
            return;
        }

        for (int i = _pages.Count - 1; i >= 0; i--)
        {
            if (_pages[i].Remove(in id))
            {
                if (_pages[i].IsEmpty)
                {
                    _pages[i].Dispose();
                    _pages.RemoveAt(i);
                }

                return;
            }
        }
    }

    private AtlasPage? FindActivePage(ref readonly AtlasKey key)
    {
        for (int i = _pages.Count - 1; i >= 0; i--)
        {
            if (!_pages[i].IsFrozen && _pages[i].Key.Equals(key))
            {
                return _pages[i];
            }
        }

        return null;
    }

    private AtlasPage? FindPage(ref readonly Guid id)
    {
        for (int i = _pages.Count - 1; i >= 0; i--)
        {
            if (_pages[i].Contains(in id))
            {
                return _pages[i];
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (SKImage image in _largeImages.Values)
        {
            image.Dispose();
        }

        _largeImages.Clear();

        foreach (AtlasPage page in _pages)
        {
            page.Dispose();
        }

        _pages.Clear();
    }
}
