using System.Collections.Generic;

namespace PdfPixel.Commands;

// TODO: [HIGH] I want separate "RestoreLayerCommand" here
/// <summary>
/// Restores the most recently saved canvas state from the state stack.
/// </summary>
public sealed class RestoreStateCommand : PdfCommand
{
    private RestoreStateCommand()
    {
    }

    /// <inheritdoc />
    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => executionContext.Frames.OnRestoreState();

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        executionContext.Canvas.Restore();
        executionContext.Frames.OnRestoreState();
    }

    /// <summary>
    /// Default instance of <see cref="RestoreStateCommand"/>.
    /// </summary>
    public static RestoreStateCommand Instance { get; } = new();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
