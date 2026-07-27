using PdfPixel.Color.Paint;
using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a path using a paint, applying the command modifier to the paint before drawing.
/// </summary>
public sealed class DrawPathCommand : PdfCommand, IPathCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given path and paint.
    /// </summary>
    public DrawPathCommand(PdfPath path, PdfPaint paint)
    {
        Path = path;
        Paint = paint;
    }

    /// <inheritdoc />
    public PdfPath Path { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawPath;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawPathCommand)} {CommandHelpers.FormatPaint(Paint)}";
}
