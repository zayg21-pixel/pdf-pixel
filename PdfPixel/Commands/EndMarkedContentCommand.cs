using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Pops the most recent marked content scope from the execution context stack.
/// </summary>
public sealed class EndMarkedContentCommand : PdfCommand
{
    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
        => executionContext.MarkedContent.Pop();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
