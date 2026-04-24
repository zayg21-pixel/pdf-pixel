using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Saves the current canvas state onto the state stack.
/// </summary>
public sealed class SaveStateCommand : PdfCommand
{
    /// <inheritdoc />
    public override Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        canvas.Save();
        return Task.CompletedTask;
    }
}
