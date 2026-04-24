using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Restores the most recently saved canvas state from the state stack.
/// </summary>
public sealed class RestoreStateCommand : PdfCommand
{
    /// <inheritdoc />
    public override Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        canvas.Restore();
        return Task.CompletedTask;
    }
}
