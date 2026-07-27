using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Restores the most recently saved canvas state from the state stack.
/// </summary>
public sealed class RestoreStateCommand : PdfCommand
{
    private RestoreStateCommand()
    {
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        executionContext.Canvas.Restore();
        executionContext.Frames.OnRestoreState();
    }

    /// <summary>
    /// Default instance of <see cref="RestoreStateCommand"/>.
    /// </summary>
    public static RestoreStateCommand Instance { get; } = new();
}
