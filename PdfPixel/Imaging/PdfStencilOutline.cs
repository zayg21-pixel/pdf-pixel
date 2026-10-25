using PdfPixel.Geometry;
using System;
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
    // Directions a crack runs along the corner grid, rows downwards, ordered clockwise so that turning right is the next one.
    private const int East = 0;
    private const int South = 1;
    private const int West = 2;
    private const int North = 3;

    private static readonly int[] ColumnSteps = [1, 0, -1, 0];
    private static readonly int[] RowSteps = [0, 1, 0, -1];

    /// <summary>
    /// Returns the outline <see cref="Trace"/> traces for <paramref name="image"/>, kept by the document once traced.
    /// </summary>
    public static PdfPath? GetOutline(PdfImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        PdfImageCache imageCache = image.Document.ObjectCache.Images;
        PdfPath? outline = imageCache.GetStencilOutline(image);

        if (outline == null)
        {
            outline = Trace(image);

            if (outline != null)
            {
                imageCache.AddStencilOutline(image, outline);
            }
        }

        return outline;
    }

    /// <summary>
    /// Returns the outline of the painted samples of <paramref name="image"/>, or null when the image is not a stencil mask
    /// or cannot be decoded.
    /// </summary>
    private static PdfPath? Trace(PdfImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (!image.HasImageMask
            || image.Width <= 0
            || image.Height <= 0)
        {
            return null;
        }

        PdfDecodedImage? samples = DecodeSamples(image);

        if (samples == null
            || samples.ColorFormat != PdfImageColorFormat.Alpha
            || samples.Width != image.Width
            || samples.Height != image.Height)
        {
            return null;
        }

        byte[] outgoingCracks = FindCracks(samples);

        return TraceContours(outgoingCracks, samples.Width, samples.Height);
    }

    /// <summary>
    /// Marks, for every corner of the sample grid, the cracks that leave it: one bit per direction, as the corner a crack
    /// starts from with the painted sample on its right.
    /// </summary>
    private static byte[] FindCracks(PdfDecodedImage samples)
    {
        int width = samples.Width;
        int height = samples.Height;
        int cornerColumns = width + 1;
        var outgoingCracks = new byte[cornerColumns * (height + 1)];
        ReadOnlySpan<byte> previousRow = default;

        for (int row = 0; row <= height; row++)
        {
            ReadOnlySpan<byte> currentRow = (row < height) ? samples.GetPixelRow(row) : default;
            int cornerRow = row * cornerColumns;

            for (int column = 0; column < width; column++)
            {
                bool paintedAbove = row > 0 && IsPainted(previousRow[column]);
                bool paintedBelow = row < height && IsPainted(currentRow[column]);

                if (paintedBelow && !paintedAbove)
                {
                    outgoingCracks[cornerRow + column] |= 1 << East;
                }
                else if (paintedAbove && !paintedBelow)
                {
                    outgoingCracks[cornerRow + column + 1] |= 1 << West;
                }
            }

            if (row == height)
            {
                break;
            }

            for (int column = 0; column <= width; column++)
            {
                bool paintedLeft = column > 0 && IsPainted(currentRow[column - 1]);
                bool paintedRight = column < width && IsPainted(currentRow[column]);

                if (paintedRight && !paintedLeft)
                {
                    outgoingCracks[cornerRow + cornerColumns + column] |= 1 << North;
                }
                else if (paintedLeft && !paintedRight)
                {
                    outgoingCracks[cornerRow + column] |= 1 << South;
                }
            }

            previousRow = currentRow;
        }

        return outgoingCracks;
    }

    /// <summary>
    /// Follows the cracks into closed contours, each started from the first corner in row order that still has a crack
    /// leaving it, which is always a corner the contour turns at. Consumes <paramref name="outgoingCracks"/>.
    /// </summary>
    private static PdfPath TraceContours(byte[] outgoingCracks, int width, int height)
    {
        int cornerColumns = width + 1;
        PdfMatrix sampleToUnitSquare = new(1f / width, 0, 0, 0, -1f / height, 1);
        PdfPathBuilder builder = new(sampleToUnitSquare);

        for (int startCorner = 0; startCorner < outgoingCracks.Length; startCorner++)
        {
            while (outgoingCracks[startCorner] != 0)
            {
                int column = startCorner % cornerColumns;
                int row = startCorner / cornerColumns;
                int direction = GetFirstDirection(outgoingCracks[startCorner]);
                int corner = startCorner;

                builder.MoveTo(column, row);

                while (true)
                {
                    outgoingCracks[corner] &= (byte)~(1 << direction);
                    column += ColumnSteps[direction];
                    row += RowSteps[direction];
                    corner = (row * cornerColumns) + column;

                    if (corner == startCorner || outgoingCracks[corner] == 0)
                    {
                        break;
                    }

                    int nextDirection = ChooseNextDirection(outgoingCracks[corner], direction);

                    if (nextDirection != direction)
                    {
                        builder.LineTo(column, row);
                    }

                    direction = nextDirection;
                }

                builder.Close();
            }
        }

        return builder.ToPath(PdfPathFillType.Winding);
    }

    /// <summary>
    /// Returns the direction to leave a corner by after arriving along <paramref name="direction"/>. A corner where two
    /// painted samples touch only diagonally has two cracks leaving it, and the contour turns right there; any pairing of
    /// the cracks fills the same, since every crack is used once with the painted sample on its right.
    /// </summary>
    private static int ChooseNextDirection(byte cracks, int direction)
    {
        int right = (direction + 1) & 3;

        if ((cracks & (1 << right)) != 0)
        {
            return right;
        }

        if ((cracks & (1 << direction)) != 0)
        {
            return direction;
        }

        return (direction + 3) & 3;
    }

    private static int GetFirstDirection(byte cracks)
    {
        for (int direction = East; direction <= North; direction++)
        {
            if ((cracks & (1 << direction)) != 0)
            {
                return direction;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(cracks), "A corner without cracks has no direction to leave by.");
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
