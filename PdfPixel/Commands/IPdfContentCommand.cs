using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Carries the bounds of one painting operation in pre-CTM space, mapped through <see cref="IMatrixCommand.Matrix"/>.
/// </summary>
public interface IPdfContentCommand : IPdfCommand, IMatrixCommand
{
    /// <summary>
    /// Kind of the painted content.
    /// </summary>
    PdfContentKind ContentKind { get; }

    /// <summary>
    /// Area the painted content covers, in pre-CTM space.
    /// </summary>
    PdfRectangle Bounds { get; }
}
