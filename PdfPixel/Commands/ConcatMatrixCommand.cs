using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Concatenates a matrix onto the current canvas transformation.
/// </summary>
public sealed class ConcatMatrixCommand : PdfCommand
{

    public ConcatMatrixCommand(SKMatrix matrix) => Matrix = matrix;

    /// <summary>
    /// Gets the matrix that this command concatenates onto the canvas.
    /// </summary>
    public SKMatrix Matrix { get; }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext) => canvas.Concat(Matrix);

    protected override void Dispose(bool disposing)
    {
    }
}
