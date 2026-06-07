using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Restores the most recently saved canvas state from the state stack.
/// </summary>
public sealed class RestoreStateCommand : PdfCommand
{
    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        executionContext.Canvas.Restore();
        executionContext.Frames.OnRestoreState();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
