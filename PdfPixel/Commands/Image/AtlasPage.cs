using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

/// <summary>
/// A single atlas page that collects small images of the same pixel format.
/// In the mutable phase, images are stored individually with pre-computed positions.
/// On freeze, all images are drawn onto a single surface and snapshotted into one atlas image.
/// </summary>
internal sealed class AtlasPage : IDisposable
{
    private const int MaxSize = 1024;
    private const int Padding = 1;

    private readonly AtlasKey _key;
    private readonly Dictionary<Guid, AtlasEntry> _entries = [];

    private SKImage? _frozenImage;
    private int _liveSegmentCount;

    private int _currentX;
    private int _currentY;
    private int _currentRowHeight;
    private int _usedWidth;
    private int _usedHeight;

    public AtlasPage(ref readonly AtlasKey key) => _key = key;

    public AtlasKey Key => _key;

    public bool IsFrozen => _frozenImage != null;

    /// <summary>
    /// Tries to add an image to this page. Returns false if there is no room.
    /// The image is copied — the caller may dispose the original after this call.
    /// </summary>
    public bool TryAdd(ref readonly Guid id, SKImage image)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("Cannot add images to a frozen atlas page.");
        }

        if (!TryComputePosition(image.Width, image.Height, out SKRectI position))
        {
            return false;
        }

        _entries[id] = new AtlasEntry(image, position);
        return true;
    }

    /// <summary>
    /// Returns true if this page contains the given id.
    /// </summary>
    public bool Contains(ref readonly Guid id) => _entries.ContainsKey(id);

    /// <summary>
    /// Retrieves a cached image. If the page is not frozen, freezes it first.
    /// </summary>
    public CachedImageResult GetImage(ref readonly Guid id)
    {
        if (!_entries.TryGetValue(id, out AtlasEntry? entry))
        {
            throw new KeyNotFoundException($"Image with id {id} was not found in this atlas page.");
        }

        if (!IsFrozen)
        {
            Freeze();
        }

        if (_frozenImage == null)
        {
            throw new InvalidOperationException("Atlas page failed to freeze.");
        }

        return new CachedImageResult(_frozenImage, entry.Position, isAtlased: true);
    }

    /// <summary>
    /// Removes an image from this page.
    /// </summary>
    public bool Remove(ref readonly Guid id)
    {
        if (!_entries.TryGetValue(id, out AtlasEntry? entry))
        {
            return false;
        }

        if (IsFrozen)
        {
            _entries.Remove(id);
            _liveSegmentCount--;

            if (_liveSegmentCount <= 0)
            {
                _frozenImage?.Dispose();
                _frozenImage = null;
            }
        }
        else
        {
            entry.ClearImage();
            _entries.Remove(id);
        }

        return true;
    }

    /// <summary>
    /// Freezes this page: draws all images onto a single surface, snapshots it,
    /// and disposes the individual images.
    /// </summary>
    public void Freeze()
    {
        if (IsFrozen)
        {
            return;
        }

        if (_entries.Count == 0)
        {
            return;
        }

        SKImageInfo info = new(_usedWidth, _usedHeight, _key.ColorType, _key.AlphaType);
        using SKSurface surface = SKSurface.Create(info);
        SKCanvas canvas = surface.Canvas;

        foreach (AtlasEntry entry in _entries.Values)
        {
            if (entry.Image != null)
            {
                using SKShader shader = entry.Image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
                using SKPaint paint = new() { Shader = shader };

                canvas.Save();
                canvas.Translate(entry.Position.Left, entry.Position.Top);
                canvas.DrawRect(new SKRect(-Padding, -Padding, entry.Image.Width + Padding, entry.Image.Height + Padding), paint);
                canvas.Restore();

                entry.ClearImage();
            }
        }

        _frozenImage = surface.Snapshot();
        _liveSegmentCount = _entries.Count;
    }

    /// <summary>
    /// True when this page has been frozen and all segments have been removed.
    /// The page can be discarded.
    /// </summary>
    public bool IsEmpty => IsFrozen && _liveSegmentCount <= 0;

    /// <inheritdoc />
    public void Dispose()
    {
        _frozenImage?.Dispose();
        _frozenImage = null;

        foreach (AtlasEntry entry in _entries.Values)
        {
            entry.ClearImage();
        }

        _entries.Clear();
    }

    private bool TryComputePosition(int width, int height, out SKRectI position)
    {
        int paddedWidth = width + (2 * Padding);
        int paddedHeight = height + (2 * Padding);

        if (_currentX + paddedWidth > MaxSize)
        {
            _currentX = 0;
            _currentY += _currentRowHeight;
            _currentRowHeight = 0;
        }

        if (_currentY + paddedHeight > MaxSize)
        {
            position = default;
            return false;
        }

        position = SKRectI.Create(_currentX + Padding, _currentY + Padding, width, height);

        _currentX += paddedWidth;

        if (paddedHeight > _currentRowHeight)
        {
            _currentRowHeight = paddedHeight;
        }

        if (_currentX > _usedWidth)
        {
            _usedWidth = _currentX;
        }

        int usedBottom = _currentY + _currentRowHeight;
        if (usedBottom > _usedHeight)
        {
            _usedHeight = usedBottom;
        }

        return true;
    }

    private sealed class AtlasEntry
    {
        public AtlasEntry(SKImage image, SKRectI position)
        {
            Image = image;
            Position = position;
        }

        public SKImage? Image { get; private set; }
        public SKRectI Position { get; }

        public void ClearImage()
        {
            Image?.Dispose();
            Image = null;
        }
    }
}
