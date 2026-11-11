using PdfPixel.Geometry;
using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Pattern;

/// <summary>
/// The rasterized window of a tiling pattern and how pattern coordinates find its pixels.
/// </summary>
internal sealed class SnapTilingWindow : IDisposable
{
    private readonly SnapMemoryBlock _colors;
    private readonly SnapMemoryBlock _alphas;
    private readonly SnapMemoryBlock _shapes;
    private readonly int _pixelCount;
    private readonly int _componentCount;

    /// <summary>
    /// Initializes a window whose planes are rented from <paramref name="scratch"/>; their values are undefined until
    /// the window is rasterized.
    /// </summary>
    public SnapTilingWindow(in SnapTilingAxis horizontal, in SnapTilingAxis vertical, int componentCount, SnapScratchBuffer scratch)
    {
        if (scratch == null)
        {
            throw new ArgumentNullException(nameof(scratch));
        }

        Horizontal = horizontal;
        Vertical = vertical;
        _componentCount = componentCount;
        _pixelCount = horizontal.Size * vertical.Size;
        _colors = scratch.MemoryPool.Rent(_pixelCount * componentCount * sizeof(float));
        _alphas = scratch.MemoryPool.Rent(_pixelCount * sizeof(float));
        _shapes = scratch.MemoryPool.Rent(_pixelCount * sizeof(float));
    }

    public SnapTilingAxis Horizontal { get; }

    public SnapTilingAxis Vertical { get; }

    public Span<float> Colors => MemoryMarshal.Cast<byte, float>(_colors.Array.AsSpan()).Slice(0, _pixelCount * _componentCount);

    public Span<float> Alphas => MemoryMarshal.Cast<byte, float>(_alphas.Array.AsSpan()).Slice(0, _pixelCount);

    public Span<float> Shapes => MemoryMarshal.Cast<byte, float>(_shapes.Array.AsSpan()).Slice(0, _pixelCount);

    /// <summary>
    /// Gets the area of pattern space the window covers.
    /// </summary>
    public PdfRectangle Area
    {
        get
        {
            return new PdfRectangle(
                Horizontal.Origin,
                Vertical.Origin,
                Horizontal.Origin + (Horizontal.Size / Horizontal.Scale),
                Vertical.Origin + (Vertical.Size / Vertical.Scale));
        }
    }

    public int GetPixel(float x, float y) => (Vertical.GetIndex(y) * Horizontal.Size) + Horizontal.GetIndex(x);

    /// <inheritdoc />
    public void Dispose()
    {
        _shapes.Dispose();
        _alphas.Dispose();
        _colors.Dispose();
    }
}
