using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// A graphics-state soft mask, drawn when it was set.
/// </summary>
internal sealed class SnapSoftMask : IPdfImageRowTarget, IDisposable
{
    private readonly SnapMemoryBlock? _block;
    private readonly int _valueCount;

    /// <summary>
    /// Initializes a mask whose values are rented from <paramref name="scratch"/>; they are undefined until written.
    /// </summary>
    public SnapSoftMask(in PdfIntegerRectangle bounds, float outsideValue, SnapScratchBuffer scratch)
    {
        if (scratch == null)
        {
            throw new ArgumentNullException(nameof(scratch));
        }

        Bounds = bounds;
        OutsideValue = outsideValue;
        _valueCount = Math.Max(0, bounds.Width) * Math.Max(0, bounds.Height);

        if (_valueCount > 0)
        {
            _block = scratch.MemoryPool.Rent(_valueCount * sizeof(float));
        }
    }

    public PdfIntegerRectangle Bounds { get; }

    /// <summary>
    /// Gets the mask value of every pixel of <see cref="Bounds"/>, row by row.
    /// </summary>
    public Span<float> Values => (_block != null) ? MemoryMarshal.Cast<byte, float>(_block.Array.AsSpan()).Slice(0, _valueCount) : Span<float>.Empty;

    /// <summary>
    /// Gets the mask value outside <see cref="Bounds"/>.
    /// </summary>
    public float OutsideValue { get; }

    /// <inheritdoc />
    public int RowBytes => Bounds.Width * sizeof(float);

    /// <inheritdoc />
    public Span<byte> GetRow(int rowIndex) => MemoryMarshal.AsBytes(Values.Slice(rowIndex * Bounds.Width, Bounds.Width));

    /// <inheritdoc />
    public void Dispose() => _block?.Dispose();
}
