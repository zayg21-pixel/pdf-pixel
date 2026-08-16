using System;
using PdfPixel.Geometry;

namespace PdfPixel.Commands;

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
    public override string ToString() => $"{nameof(DrawTilingCommand)} {CommandHelpers.FormatMatrix(Matrix)}";
}
