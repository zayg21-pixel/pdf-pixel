using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Restores the most recently saved canvas state from the state stack.
/// </summary>
public sealed class RestoreStateCommand : PdfCommand
{
    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext) => canvas.Restore();

    protected override void Dispose(bool disposing)
    {
    }
}
