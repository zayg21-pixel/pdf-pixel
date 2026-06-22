using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Pops the most recent marked content scope from the execution context stack.
/// </summary>
public sealed class EndMarkedContentCommand : PdfCommand
{
    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        if (executionContext.MarkedContentStack.Count > 0)
        {
            executionContext.MarkedContentStack.Pop();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
