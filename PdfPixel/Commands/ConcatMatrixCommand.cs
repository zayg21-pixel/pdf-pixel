using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Concatenates a matrix onto the current canvas transformation.
/// </summary>
public sealed class ConcatMatrixCommand : PdfCommand
{
    private readonly SKMatrix _matrix;

    public ConcatMatrixCommand(SKMatrix matrix)
    {
        _matrix = matrix;
    }

    /// <summary>
    /// Gets the matrix that this command concatenates onto the canvas.
    /// </summary>
    public SKMatrix Matrix => _matrix;

    /// <inheritdoc />
    public override Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        canvas.Concat(_matrix);
        return Task.CompletedTask;
    }
}
