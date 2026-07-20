using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Concatenates a matrix onto the current canvas transformation.
/// </summary>
public sealed class ConcatMatrixCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with the matrix to concatenate.
    /// </summary>
    public ConcatMatrixCommand(in PdfMatrix matrix) => Matrix = matrix;

    /// <summary>
    /// Gets the matrix that this command concatenates onto the canvas.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        executionContext.Canvas.Concat(Matrix.ToSkMatrix());
        executionContext.Frames.OnConcatMatrix(Matrix);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(ConcatMatrixCommand)} {CommandHelpers.FormatMatrix(Matrix)}";
}
