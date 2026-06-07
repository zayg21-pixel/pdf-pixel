using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Saves the current canvas state onto the state stack.
/// </summary>
public sealed class SaveStateCommand : PdfCommand
{
    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        canvas.Save();
        executionContext.Frames.OnSaveState();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
