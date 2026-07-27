using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a clipping-path operation. When <see cref="Paint"/> is a stroke paint, the path
/// should be converted to its stroke fill outline before clipping; otherwise the path is used as-is.
/// </summary>
public sealed class ClipPathCommand : PdfCommand, IPathCommand
{
    /// <summary>
    /// Initializes the command with the given path, clip operation, and optional stroke/fill paint.
    /// </summary>
    public ClipPathCommand(PdfPath path, PdfClipOperation operation, PdfPaint? paint = null)
    {
        Path = path;
        Paint = paint;
        Operation = operation;
    }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.ClipPath;

    /// <inheritdoc />
    public PdfPath Path { get; }

    /// <summary>
    /// Gets the paint that determines whether <see cref="Path"/> is clipped as a stroke outline or as-is.
    /// Null clips the path as-is.
    /// </summary>
    public PdfPaint? Paint { get; }

    /// <summary>
    /// Gets the clip operation this command applies.
    /// </summary>
    public PdfClipOperation Operation { get; }
}
