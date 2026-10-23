using PdfPixel.Commands;
using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace PdfPixel.Imaging;

/// <summary>
/// Traces the painted samples of a small stencil mask into a path in the unit square the image is drawn in, so the mask
/// can be filled as a shape: antialiased at whatever size it is drawn, instead of resampled.
/// </summary>
/// <remarks>
/// The outline follows the cracks between painted and unpainted samples on the grid of sample corners. Every crack is a
/// directed edge with the painted sample on its right, so each contour closes on itself, holes run opposite to the
/// contours around them, and the nonzero fill of all the contours covers exactly the painted samples. Only the corners
/// where a contour turns become points of the path.
/// </remarks>
internal static class PdfStencilOutline
{
    /// <summary>
    /// Largest width or height, in samples, of a stencil mask that is traced.
    /// </summary>
    public const int MaximumSize = 1000;

    /// <summary>
    /// Largest number of points a traced outline is made of; a mask needing more is left an image.
    /// </summary>
    public const int MaximumPoints = 20000;

    // Directions a crack runs along the corner grid, rows downwards, are numbered clockwise from east (east 0, south 1,
    // west 2, north 3), so that turning right is the next one; a set of cracks has one bit per direction.

    // Samples of the 2x2 neighborhood of a corner, as the bits of its index into CornerCracks.
    private const int TopLeft = 1 << 0;
    private const int BottomLeft = 1 << 2;

    private static ReadOnlySpan<sbyte> ColumnSteps => [1, 0, -1, 0];

    private static ReadOnlySpan<sbyte> RowSteps => [0, 1, 0, -1];

    /// <summary>
    /// The cracks leaving a corner, by its 2x2 neighborhood: top-left, top-right, bottom-left and bottom-right sample in
    /// bits 0 to 3, set when painted. A crack runs east under a painted bottom-right sample, south after a painted
    /// bottom-left one, west over a painted top-left one and north before a painted top-right one, each where its
    /// neighbor across the crack is unpainted.
    /// </summary>
    private static ReadOnlySpan<byte> CornerCracks => [0, 4, 8, 4, 2, 2, 10, 2, 1, 5, 8, 4, 1, 1, 8, 0];

    /// <summary>
    /// The direction to leave a corner by, indexed by the direction arrived along in bits 4 and 5 and the cracks still
    /// leaving the corner in bits 0 to 3. A corner where two painted samples touch only diagonally has two cracks leaving
    /// it, and the contour turns right there; any pairing of the cracks fills the same, since every crack is used once with
    /// the painted sample on its right.
    /// </summary>
    private static ReadOnlySpan<byte> NextDirections =>
    [
        3, 0, 1, 1, 3, 0, 1, 1, 3, 0, 1, 1, 3, 0, 1, 1,
        0, 0, 1, 1, 2, 2, 2, 2, 0, 0, 1, 1, 2, 2, 2, 2,
        1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 0, 2, 0, 2, 0, 2, 0, 3, 0, 3, 0, 3, 0, 3, 0,
    ];

    /// <summary>
    /// The first of the cracks leaving a corner in direction order, indexed by those cracks.
    /// </summary>
    private static ReadOnlySpan<byte> FirstDirections => [0, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0];

    /// <summary>
    /// Returns the outline of the painted samples of <paramref name="image"/>, or null when the image is not a stencil mask,
    /// is larger than <see cref="MaximumSize"/>, needs more than <see cref="MaximumPoints"/> points, or cannot be decoded.
    /// </summary>
    public static PdfPath? Trace(PdfImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (!image.HasImageMask
            || image.Width <= 0
            || image.Height <= 0
            || image.Width > MaximumSize
            || image.Height > MaximumSize)
        {
            return null;
        }

        PdfDecodedImage? samples = DecodeSamples(image);

        if (samples == null
            || samples.Width != image.Width
            || samples.Height != image.Height)
        {
            return null;
        }

        return Trace(samples);
    }

    /// <summary>
    /// Returns the outline of the painted samples of decoded stencil mask <paramref name="samples"/>, or null when they are
    /// not alpha samples or need more than <see cref="MaximumPoints"/> points.
    /// </summary>
    internal static PdfPath? Trace(PdfDecodedImage samples)
    {
        if (samples == null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        if (samples.ColorFormat != PdfImageColorFormat.Alpha || samples.Width <= 0 || samples.Height <= 0)
        {
            return null;
        }

        byte[] outgoingCracks = FindCracks(samples);

        return TraceContours(outgoingCracks, samples.Width, samples.Height);
    }

    /// <summary>
    /// Marks, for every corner of the sample grid, the cracks that leave it: one bit per direction, as the corner a crack
    /// starts from with the painted sample on its right. The cracks of a corner depend only on the 2x2 samples around it.
    /// </summary>
    private static byte[] FindCracks(PdfDecodedImage samples)
    {
        int width = samples.Width;
        int height = samples.Height;
        int cornerColumns = width + 1;
        var outgoingCracks = new byte[cornerColumns * (height + 1)];
        var unpaintedRow = new byte[width];
        ReadOnlySpan<byte> cornerCracks = CornerCracks;
        ref byte cornerCracksStart = ref MemoryMarshal.GetReference(cornerCracks);
        ref byte cracksStart = ref MemoryMarshal.GetReference(outgoingCracks.AsSpan());
        ReadOnlySpan<byte> rowAbove = unpaintedRow;

        for (int row = 0; row <= height; row++)
        {
            ReadOnlySpan<byte> rowBelow = (row < height) ? samples.GetPixelRow(row) : unpaintedRow;
            ref byte above = ref MemoryMarshal.GetReference(rowAbove);
            ref byte below = ref MemoryMarshal.GetReference(rowBelow);
            ref byte cracks = ref Unsafe.Add(ref cracksStart, row * cornerColumns);
            int neighborhood = 0;

            for (int column = 0; column < width; column++)
            {
                neighborhood = ((neighborhood >> 1) & (TopLeft | BottomLeft))
                    | ((Unsafe.Add(ref above, column) >> 7) << 1)
                    | ((Unsafe.Add(ref below, column) >> 7) << 3);
                Unsafe.Add(ref cracks, column) = Unsafe.Add(ref cornerCracksStart, neighborhood);
            }

            neighborhood = (neighborhood >> 1) & (TopLeft | BottomLeft);
            Unsafe.Add(ref cracks, width) = Unsafe.Add(ref cornerCracksStart, neighborhood);
            rowAbove = rowBelow;
        }

        return outgoingCracks;
    }

    /// <summary>
    /// Follows the cracks into closed contours, each started from the first corner in row order that still has a crack
    /// leaving it, which is always a corner the contour turns at. Consumes <paramref name="outgoingCracks"/>.
    /// </summary>
    private static PdfPath? TraceContours(byte[] outgoingCracks, int width, int height)
    {
        int cornerColumns = width + 1;
        PdfMatrix sampleToUnitSquare = new(1f / width, 0, 0, 0, -1f / height, 1);
        PdfPathBuilder builder = new(sampleToUnitSquare);
        Span<int> cornerSteps = stackalloc int[] { 1, cornerColumns, -1, -cornerColumns };
        ref int cornerStepsStart = ref MemoryMarshal.GetReference(cornerSteps);
        ref sbyte columnStepsStart = ref MemoryMarshal.GetReference(ColumnSteps);
        ref sbyte rowStepsStart = ref MemoryMarshal.GetReference(RowSteps);
        ref byte nextDirectionsStart = ref MemoryMarshal.GetReference(NextDirections);
        ref byte firstDirectionsStart = ref MemoryMarshal.GetReference(FirstDirections);
        ref byte cracksStart = ref MemoryMarshal.GetReference(outgoingCracks.AsSpan());
        int pointCount = 0;
        int startCorner = 0;

        while (true)
        {
            int nextStart = FindCornerWithCracks(outgoingCracks, startCorner);

            if (nextStart < 0)
            {
                break;
            }

            startCorner = nextStart;
            byte startCracks = Unsafe.Add(ref cracksStart, startCorner);
            int column = startCorner % cornerColumns;
            int row = startCorner / cornerColumns;
            int direction = Unsafe.Add(ref firstDirectionsStart, startCracks);
            int corner = startCorner;

            builder.MoveTo(column, row);
            pointCount++;

            while (true)
            {
                ref byte cornerCracks = ref Unsafe.Add(ref cracksStart, corner);
                cornerCracks &= (byte)~(1 << direction);
                corner += Unsafe.Add(ref cornerStepsStart, direction);
                column += Unsafe.Add(ref columnStepsStart, direction);
                row += Unsafe.Add(ref rowStepsStart, direction);

                int cracks = Unsafe.Add(ref cracksStart, corner);

                if (corner == startCorner || cracks == 0)
                {
                    break;
                }

                int nextDirection = Unsafe.Add(ref nextDirectionsStart, (direction << 4) | cracks);

                if (nextDirection != direction)
                {
                    if (++pointCount > MaximumPoints)
                    {
                        return null;
                    }

                    builder.LineTo(column, row);
                    direction = nextDirection;
                }
            }

            builder.Close();
        }

        return builder.Detach(PdfPathFillType.Winding);
    }

    /// <summary>
    /// Returns the first corner from <paramref name="start"/> on that still has a crack leaving it, or -1 when none has.
    /// </summary>
    private static int FindCornerWithCracks(byte[] outgoingCracks, int start)
    {
        ReadOnlySpan<byte> remaining = outgoingCracks.AsSpan(start);
#if NET8_0_OR_GREATER
        int index = remaining.IndexOfAnyExcept((byte)0);
#else
        int index = -1;

        for (int position = 0; position < remaining.Length; position++)
        {
            if (remaining[position] != 0)
            {
                index = position;
                break;
            }
        }
#endif

        return (index < 0) ? -1 : start + index;
    }

    private static PdfDecodedImage? DecodeSamples(PdfImage image)
    {
        PdfCancellationExecutionObserver observer = new(CancellationToken.None);

        try
        {
            return image.DecodePixels(PdfImageDecodingParameters.Default, observer);
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            // Left an image, whose drawing reports what is wrong with its data.
            return null;
        }
#pragma warning restore CA1031
    }

    private static bool IsPainted(byte alpha) => alpha >= 128;
}
