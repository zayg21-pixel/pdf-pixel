using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Saves the current canvas state onto the state stack.
/// </summary>
public sealed class SaveStateCommand : PdfCommand
{
    private SaveStateCommand()
    {
    }

    /// <inheritdoc />
    public override void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => executionContext.Frames.OnSaveState();

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        executionContext.Canvas.Save();
        executionContext.Frames.OnSaveState();
    }

    /// <summary>
    /// Default instance of <see cref="SaveStateCommand"/>.
    /// </summary>
    public static SaveStateCommand Instance { get; } = new();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
