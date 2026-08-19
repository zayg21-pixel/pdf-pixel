using PdfPixel.Geometry;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Concatenates a matrix onto the current transformation.
/// </summary>
public sealed class ConcatMatrixCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with the matrix to concatenate.
    /// </summary>
    public ConcatMatrixCommand(in PdfMatrix matrix) => Matrix = matrix;

    /// <summary>
    /// Gets the matrix that this command concatenates onto the current transformation.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.ConcatMatrix;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(ConcatMatrixCommand)} {PdfCommandFormatting.FormatMatrix(Matrix)}";
}
