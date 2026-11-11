using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// How a group source repeats its recording: one cell, clipped to its bounding box, at every whole multiple of the
/// steps from the origin of the recording's space.
/// </summary>
public sealed class PdfTiling
{
    /// <summary>
    /// Initializes the tiling with the cell's bounding box and the spacing between cells.
    /// </summary>
    /// <param name="boundingBox">The cell's bounding box, in the recording's space.</param>
    /// <param name="xStep">The horizontal spacing between cells.</param>
    /// <param name="yStep">The vertical spacing between cells.</param>
    public PdfTiling(in PdfRectangle boundingBox, float xStep, float yStep)
    {
        BoundingBox = boundingBox;
        XStep = xStep;
        YStep = yStep;
    }

    /// <summary>
    /// Gets the cell's bounding box, in the recording's space, that each cell is clipped to.
    /// </summary>
    public PdfRectangle BoundingBox { get; }

    /// <summary>
    /// Gets the horizontal spacing between cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Returns <paramref name="area"/>, in the recording's space, expanded to whole steps.
    /// </summary>
    /// <param name="area">The area to cover with cells.</param>
    public PdfRectangle GetTilingArea(in PdfRectangle area)
    {
        float startX = MathF.Floor(area.Left / XStep) * XStep;
        float startY = MathF.Floor(area.Top / YStep) * YStep;
        float endX = MathF.Ceiling(area.Right / XStep) * XStep;
        float endY = MathF.Ceiling(area.Bottom / YStep) * YStep;

        return new PdfRectangle(startX, startY, endX, endY);
    }

    /// <summary>
    /// Returns the cells covering <paramref name="area"/> as inclusive cell indices, columns on the
    /// left and right edges and rows on the top and bottom. A cell sits at whole multiples of the
    /// step from the pattern space origin and belongs to the grid when its bounding box reaches into
    /// the area, so a cell wider than its step brings in the neighbours that overlap it. A step that
    /// does not advance leaves the single cell at the origin.
    /// </summary>
    public PdfIntegerRectangle ComputeCellGrid(in PdfRectangle area)
    {
        int firstColumn = 0;
        int lastColumn = 0;

        if (XStep > 0)
        {
            firstColumn = (int)MathF.Floor((area.Left - BoundingBox.Right) / XStep) + 1;
            lastColumn = (int)MathF.Ceiling((area.Right - BoundingBox.Left) / XStep) - 1;
        }

        int firstRow = 0;
        int lastRow = 0;

        if (YStep > 0)
        {
            firstRow = (int)MathF.Floor((area.Top - BoundingBox.Bottom) / YStep) + 1;
            lastRow = (int)MathF.Ceiling((area.Bottom - BoundingBox.Top) / YStep) - 1;
        }

        return new PdfIntegerRectangle(firstColumn, firstRow, lastColumn, lastRow);
    }

    /// <summary>
    /// Returns the area the cells of <paramref name="cellGrid"/> reach, in the recording's space.
    /// </summary>
    /// <param name="cellGrid">Inclusive cell indices, as <see cref="ComputeCellGrid"/> returns them.</param>
    public PdfRectangle GetPaintedArea(in PdfIntegerRectangle cellGrid)
    {
        return new(
            (cellGrid.Left * XStep) + BoundingBox.Left,
            (cellGrid.Top * YStep) + BoundingBox.Top,
            (cellGrid.Right * XStep) + BoundingBox.Right,
            (cellGrid.Bottom * YStep) + BoundingBox.Bottom);
    }
}
