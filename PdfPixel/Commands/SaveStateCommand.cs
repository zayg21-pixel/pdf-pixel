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
    public override void Initialize(PdfCommandExecutionContext executionContext)
        => executionContext.Frames.OnSaveState();

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
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
