using PdfPixel.Commands.Cache;
using PdfPixel.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Snap.Rasterization;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Text;

/// <summary>
/// The glyph stencils of one typeface, one per glyph and subpixel position, re-rasterized when the glyph is drawn at
/// another size.
/// </summary>
internal sealed class SnapGlyphStencilCache : ICommandCacheItem
{
    /// <summary>
    /// The subpixel positions per axis a glyph's origin is placed at.
    /// </summary>
    public const int SubpixelSteps = 4;

    private readonly Dictionary<(ushort GlyphId, int SubpixelX, int SubpixelY), Slot> _slots = [];

    private SnapGlyphStencilCache(IPdfTypeface typeface) => Typeface = typeface;

    public IPdfTypeface Typeface { get; }

    /// <summary>
    /// Returns the stencil cache of <paramref name="typeface"/> in <paramref name="cache"/>, creating it on first use.
    /// </summary>
    public static SnapGlyphStencilCache Get(CommandCache cache, IPdfTypeface typeface)
    {
        CacheKey key = new(typeface);

        if (cache.GetEntry(key) is SnapGlyphStencilCache existing)
        {
            return existing;
        }

        SnapGlyphStencilCache created = new(typeface);
        cache.StoreEntry(key, created);

        return created;
    }

    /// <summary>
    /// Returns the stencil of <paramref name="glyphId"/> at a subpixel position in 1/<see cref="SubpixelSteps"/> steps,
    /// ignoring the translation of <paramref name="glyphToDevice"/>, or <see langword="null"/> when it has no outline.
    /// </summary>
    public SnapStencil? GetStencil(ushort glyphId, in PdfMatrix glyphToDevice, int subpixelX, int subpixelY, bool areaCoverage, SnapEdgeList edges, SnapRasterizer rasterizer)
    {
        (ushort GlyphId, int SubpixelX, int SubpixelY) slotKey = (glyphId, subpixelX, subpixelY);

        if (_slots.TryGetValue(slotKey, out Slot slot) && slot.IsFor(glyphToDevice, areaCoverage))
        {
            return slot.Stencil;
        }

        SnapStencil? stencil = Rasterize(glyphId, glyphToDevice, subpixelX, subpixelY, areaCoverage, edges, rasterizer);
        _slots[slotKey] = new Slot(glyphToDevice, areaCoverage, stencil);

        return stencil;
    }

    /// <inheritdoc />
    public void Dispose() => _slots.Clear();

    private SnapStencil? Rasterize(ushort glyphId, in PdfMatrix glyphToDevice, int subpixelX, int subpixelY, bool areaCoverage, SnapEdgeList edges, SnapRasterizer rasterizer)
    {
        ReadOnlyMemory<byte> pathData = Typeface.GetPath(glyphId);

        if (pathData.IsEmpty)
        {
            return null;
        }

        PdfPath outline = new(pathData, PdfPathFillType.Winding);
        PdfMatrix placement = new(
            glyphToDevice.ScaleX,
            glyphToDevice.SkewX,
            subpixelX / (float)SubpixelSteps,
            glyphToDevice.SkewY,
            glyphToDevice.ScaleY,
            subpixelY / (float)SubpixelSteps);

        edges.Clear();
        edges.AddPath(outline, placement);

        if (edges.Edges.Length == 0)
        {
            return null;
        }

        PdfIntegerRectangle bounds = PdfSnapGrid.RoundOut(new PdfRectangle(edges.MinX, edges.MinY, edges.MaxX, edges.MaxY));

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        SnapStencilSink sink = new(bounds);
        rasterizer.Rasterize(edges, PdfPathFillType.Winding, areaCoverage, bounds, ref sink);

        return sink.ToStencil();
    }

    private readonly struct Slot
    {
        private readonly float _scaleX;
        private readonly float _skewX;
        private readonly float _skewY;
        private readonly float _scaleY;
        private readonly bool _areaCoverage;

        public Slot(in PdfMatrix glyphToDevice, bool areaCoverage, SnapStencil? stencil)
        {
            _scaleX = glyphToDevice.ScaleX;
            _skewX = glyphToDevice.SkewX;
            _skewY = glyphToDevice.SkewY;
            _scaleY = glyphToDevice.ScaleY;
            _areaCoverage = areaCoverage;
            Stencil = stencil;
        }

        public SnapStencil? Stencil { get; }

        public bool IsFor(in PdfMatrix glyphToDevice, bool areaCoverage)
        {
            return _scaleX == glyphToDevice.ScaleX
                && _skewX == glyphToDevice.SkewX
                && _skewY == glyphToDevice.SkewY
                && _scaleY == glyphToDevice.ScaleY
                && _areaCoverage == areaCoverage;
        }
    }

    private sealed class CacheKey : ICommandCacheKey
    {
        private readonly IPdfTypeface _typeface;

        public CacheKey(IPdfTypeface typeface) => _typeface = typeface;

        public bool Equals(ICommandCacheKey? other) => other is CacheKey key && ReferenceEquals(_typeface, key._typeface);

        public override bool Equals(object? obj) => obj is CacheKey key && ReferenceEquals(_typeface, key._typeface);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(_typeface);
    }
}
