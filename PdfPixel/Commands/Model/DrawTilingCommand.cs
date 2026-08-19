using System;
using PdfPixel.Geometry;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Represents a recorded pattern cell replayed across a tiled grid covering the given bounds, under its own matrix.
/// A save/restore boundary surrounds the replay because it draws a nested <see cref="DrawRecordingCommand"/>
/// directly in pattern space.
/// </summary>
public sealed class DrawTilingCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with the matrix, the pattern-space bounds to tile, the cell bounding box, the cell step, and the recorded cell content.
    /// </summary>
    public DrawTilingCommand(in PdfMatrix matrix, in PdfRectangle bounds, in PdfRectangle bbox, float xStep, float yStep, DrawRecordingCommand recordingCommand)
    {
        Matrix = matrix;
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        RecordingCommand = recordingCommand;

        float startX = MathF.Floor(bounds.Left / xStep) * xStep;
        float startY = MathF.Floor(bounds.Top / yStep) * yStep;
        float endX = MathF.Ceiling(bounds.Right / xStep) * xStep;
        float endY = MathF.Ceiling(bounds.Bottom / yStep) * yStep;

        TilingArea = new PdfRectangle(startX, startY, endX, endY);
        CellGrid = ComputeCellGrid(TilingArea);

        PaintedArea = new PdfRectangle(
            (CellGrid.Left * xStep) + bbox.Left,
            (CellGrid.Top * yStep) + bbox.Top,
            (CellGrid.Right * xStep) + bbox.Right,
            (CellGrid.Bottom * yStep) + bbox.Bottom);
    }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the pattern cell's bounding box, in pattern space, that each tile is clipped to.
    /// </summary>
    public PdfRectangle BBox { get; }

    /// <summary>
    /// Gets the pattern-space area to cover with tiles, expanded to whole steps.
    /// </summary>
    public PdfRectangle TilingArea { get; }

    /// <summary>
    /// Gets the cells covering <see cref="TilingArea"/>, as inclusive cell indices.
    /// </summary>
    public PdfIntegerRectangle CellGrid { get; }

    /// <summary>
    /// Gets the area the cells of <see cref="CellGrid"/> reach, in pattern space.
    /// </summary>
    public PdfRectangle PaintedArea { get; }

    /// <summary>
    /// Gets the horizontal spacing between pattern cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between pattern cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Gets the recorded pattern cell replayed at each tile position.
    /// </summary>
    public DrawRecordingCommand RecordingCommand { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => RecordingCommand.Features;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawTiling;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawTilingCommand)} {PdfCommandFormatting.FormatMatrix(Matrix)}";

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
            firstColumn = (int)MathF.Floor((area.Left - BBox.Right) / XStep) + 1;
            lastColumn = (int)MathF.Ceiling((area.Right - BBox.Left) / XStep) - 1;
        }

        int firstRow = 0;
        int lastRow = 0;

        if (YStep > 0)
        {
            firstRow = (int)MathF.Floor((area.Top - BBox.Bottom) / YStep) + 1;
            lastRow = (int)MathF.Ceiling((area.Bottom - BBox.Top) / YStep) - 1;
        }

        return new PdfIntegerRectangle(firstColumn, firstRow, lastColumn, lastRow);
    }
}
