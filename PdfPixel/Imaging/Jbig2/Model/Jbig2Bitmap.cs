using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jbig2.Model;

/// <summary>
/// Represents a JBIG2 bitmap (bi-level image buffer).
/// Stores pixels as packed bytes (MSB-first), with 1 representing the foreground (typically black).
/// </summary>
internal sealed class Jbig2Bitmap
{
    private readonly byte[] _data;

    /// <summary>
    /// Shared 1×1 empty placeholder bitmap for invalid/missing symbols.
    /// </summary>
    public static Jbig2Bitmap Empty { get; } = new Jbig2Bitmap(1, 1);

    /// <summary>
    /// Width of the bitmap in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the bitmap in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Number of bytes per row (stride), including padding to byte boundaries.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Initializes a new bitmap with the specified dimensions, filled with the given default pixel value.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="defaultPixel">Default pixel value (0 or 1).</param>
    public Jbig2Bitmap(int width, int height, byte defaultPixel = 0)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
        Stride = (width + 7) >> 3;
        _data = new byte[Stride * height];

        if (defaultPixel != 0)
        {
            _data.AsSpan().Fill(0xFF);
        }
    }

    /// <summary>
    /// Gets a span over the underlying packed bitmap data.
    /// </summary>
    public Span<byte> Data => _data;

    /// <summary>
    /// Gets a read-only span over the underlying packed bitmap data.
    /// </summary>
    public ReadOnlySpan<byte> ReadOnlyData => _data;

    /// <summary>
    /// Gets a span for a single row of packed pixel data.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <returns>Span covering the row's bytes.</returns>
    public Span<byte> GetRow(int row) => _data.AsSpan(row * Stride, Stride);

    /// <summary>
    /// Gets a read-only span for a single row of packed pixel data.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <returns>Read-only span covering the row's bytes.</returns>
    public ReadOnlySpan<byte> GetRowReadOnly(int row) => _data.AsSpan(row * Stride, Stride);

    /// <summary>
    /// Gets the pixel value at the given coordinates.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <returns>0 or 1.</returns>
    public int GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return 0;
        }

        int byteIndex = y * Stride + (x >> 3);
        int bitIndex = 7 - (x & 7);
        return (_data[byteIndex] >> bitIndex) & 1;
    }

    /// <summary>
    /// Sets the pixel value at the given coordinates.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <param name="value">Pixel value (0 or 1).</param>
    public void SetPixel(int x, int y, int value)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return;
        }

        int byteIndex = y * Stride + (x >> 3);
        int bitIndex = 7 - (x & 7);

        if (value != 0)
        {
            _data[byteIndex] |= (byte)(1 << bitIndex);
        }
        else
        {
            _data[byteIndex] &= (byte)~(1 << bitIndex);
        }
    }

    /// <summary>
    /// Composites the source bitmap onto this bitmap at the specified location using the given operator.
    /// </summary>
    /// <param name="source">Source bitmap to composite.</param>
    /// <param name="x">Horizontal offset on this bitmap.</param>
    /// <param name="y">Vertical offset on this bitmap.</param>
    /// <param name="op">Combination operator.</param>
    public void Composite(Jbig2Bitmap source, int x, int y, Jbig2CombinationOperator op)
        => Composite(source, x, y, op, 0, 0, Width, Height);

    /// <summary>
    /// Composites the source bitmap onto this bitmap at the specified location using the given
    /// operator, clipped to the supplied destination sub-rectangle (intersected with this bitmap's
    /// bounds). Pixels falling outside the clip rectangle are left untouched. Used by the
    /// text-region direct-compose fast path to enforce per-region clipping while writing straight
    /// to the page bitmap.
    /// </summary>
    public void Composite(
        Jbig2Bitmap source,
        int x,
        int y,
        Jbig2CombinationOperator op,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
        // TODO: [HIGH] I still don't like it, would better prefer more advanced bit reader/writer
        if (source == null)
        {
            return;
        }

        // Effective destination bounds = intersection of this bitmap's extent and the clip rect.
        int dstLeft = clipX > 0 ? clipX : 0;
        int dstTop = clipY > 0 ? clipY : 0;
        int dstRight = clipX + clipWidth < Width ? clipX + clipWidth : Width;
        int dstBottom = clipY + clipHeight < Height ? clipY + clipHeight : Height;

        // Clamp source range to the effective destination bounds.
        int srcYStart = 0;
        int srcYEnd = source.Height;

        if (y < dstTop)
        {
            srcYStart = dstTop - y;
        }

        if (y + srcYEnd > dstBottom)
        {
            srcYEnd = dstBottom - y;
        }

        int srcXStart = 0;
        int srcXEnd = source.Width;

        if (x < dstLeft)
        {
            srcXStart = dstLeft - x;
        }

        if (x + srcXEnd > dstRight)
        {
            srcXEnd = dstRight - x;
        }

        if (srcXStart >= srcXEnd || srcYStart >= srcYEnd)
        {
            return;
        }

        int dstXStart = x + srcXStart;
        int pixelCount = srcXEnd - srcXStart;
        var operation = ApplyOperatorFunction(op);

        for (int srcY = srcYStart; srcY < srcYEnd; srcY++)
        {
            int dstY = y + srcY;
            var srcRow = source.GetRowReadOnly(srcY);
            var dstRow = GetRow(dstY);
            int srcByteIdx = srcXStart >> 3;
            int srcBitOffset = srcXStart & 7;
            int dstByteIdx = dstXStart >> 3;
            int dstBitOffset = dstXStart & 7;

            // Sliding window: the next source bit to consume is always in the MSB (bit 31).
            // windowBits tracks how many valid bits are currently loaded.
            uint window = 0;
            int windowBits = 0;

            // Prime the window and discard the leading srcBitOffset bits.
            while (windowBits <= 24 && srcByteIdx < srcRow.Length)
            {
                window |= (uint)srcRow[srcByteIdx++] << (24 - windowBits);
                windowBits += 8;
            }

            window <<= srcBitOffset;
            windowBits -= srcBitOffset;

            int bitsRemaining = pixelCount;

            while (bitsRemaining > 0)
            {
                // Refill window to keep at least 8 bits available for the next destination byte.
                while (windowBits <= 24 && srcByteIdx < srcRow.Length)
                {
                    window |= (uint)srcRow[srcByteIdx++] << (24 - windowBits);
                    windowBits += 8;
                }

                // Number of bits to write into the current destination byte.
                int bitsThisByte = Math.Min(8 - dstBitOffset, bitsRemaining);

                // Bit mask for the region being written inside the destination byte (MSB-first).
                int mask = ((1 << bitsThisByte) - 1) << (8 - dstBitOffset - bitsThisByte);

                // Extract the top 8 bits of the window and shift them to align with dstBitOffset.
                byte srcAligned = (byte)((window >> 24) >> dstBitOffset);

                byte dst = dstRow[dstByteIdx];
                byte applied = operation(dst, srcAligned);

                // Write the operated bits inside the mask; preserve bits outside it.
                dstRow[dstByteIdx] = (byte)((applied & mask) | (dst & ~mask));

                window <<= bitsThisByte;
                windowBits -= bitsThisByte;
                bitsRemaining -= bitsThisByte;
                dstByteIdx++;
                dstBitOffset = 0;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Func<byte, byte, byte> ApplyOperatorFunction(Jbig2CombinationOperator op)
    {
        return op switch
        {
            Jbig2CombinationOperator.Or => (dst, src) => (byte)(dst | src),
            Jbig2CombinationOperator.And => (dst, src) => (byte)(dst & src),
            Jbig2CombinationOperator.Xor => (dst, src) => (byte)(dst ^ src),
            Jbig2CombinationOperator.Xnor => (dst, src) => (byte)~(dst ^ src),
            Jbig2CombinationOperator.Replace => (dst, src) => src,
            _ => (dst, src) => src
        };
    }
}
