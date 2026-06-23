using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Pops the most recent marked content scope from the execution context stack.
/// </summary>
public sealed class EndMarkedContentCommand : PdfCommand
{
    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => executionContext.MarkedContent.Pop();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
