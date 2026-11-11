using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a path using a paint.
/// </summary>
public sealed class DrawPathCommand : PdfCommand, IPathCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given path and paint.
    /// </summary>
    public DrawPathCommand(PdfPath path, PdfPaint paint)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Paint = paint ?? throw new ArgumentNullException(nameof(paint));
        Bounds = (paint.Style == PdfPaintStyle.Stroke) ? path.GetStrokeBounds(paint) : path.GetBounds();
    }

    /// <summary>
    /// Initializes the command with the given path, paint and group source.
    /// </summary>
    public DrawPathCommand(PdfPath path, PdfPaint paint, PdfGroupSource source)
        : this(path, paint)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <inheritdoc />
    public PdfPath Path { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public PdfRectangle? Bounds { get; }

    /// <summary>
    /// Gets the group the path is painted with, or <see langword="null"/> when it is painted in the paint's color.
    /// </summary>
    public PdfGroupSource? Source { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawPath;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawPathCommand)} {PdfCommandFormatting.FormatPaint(Paint)}{((Source != null) ? " " + Source : string.Empty)}";
}
