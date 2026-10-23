using PdfPixel.Geometry;
using PdfPixel.Imaging;
using System;

namespace Benchmarks
{
    /// <summary>
    /// The stencil outline tracing as first committed, kept as the baseline the library implementation is measured and
    /// checked against.
    /// </summary>
    internal static class StencilOutlineBaseline
    {
        private const int MaximumPoints = 20000;

        private const int East = 0;
        private const int South = 1;
        private const int West = 2;
        private const int North = 3;

        private static readonly int[] ColumnSteps = [1, 0, -1, 0];
        private static readonly int[] RowSteps = [0, 1, 0, -1];

        public static PdfPath? Trace(PdfDecodedImage samples)
        {
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
        private static PdfPath? TraceContours(byte[] outgoingCracks, int width, int height)
        {
            int cornerColumns = width + 1;
            PdfMatrix sampleToUnitSquare = new(1f / width, 0, 0, 0, -1f / height, 1);
            PdfPathBuilder builder = new(sampleToUnitSquare);
            int pointCount = 0;

            for (int startCorner = 0; startCorner < outgoingCracks.Length; startCorner++)
            {
                while (outgoingCracks[startCorner] != 0)
                {
                    int column = startCorner % cornerColumns;
                    int row = startCorner / cornerColumns;
                    int direction = GetFirstDirection(outgoingCracks[startCorner]);
                    int corner = startCorner;

                    builder.MoveTo(column, row);
                    pointCount++;

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
                            pointCount++;
                        }

                        direction = nextDirection;
                    }

                    builder.Close();

                    if (pointCount > MaximumPoints)
                    {
                        return null;
                    }
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

        private static bool IsPainted(byte alpha) => alpha >= 128;
    }
}
