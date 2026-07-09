using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Restores the most recently saved layer from the state stack, mirroring <see cref="SaveLayerCommand"/>.
/// </summary>
public sealed class RestoreLayerCommand : PdfCommand
{
    private RestoreLayerCommand()
    {
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        executionContext.Canvas.Restore();
        executionContext.Frames.OnRestoreState();
    }

    /// <summary>
    /// Default instance of <see cref="RestoreLayerCommand"/>.
    /// </summary>
    public static RestoreLayerCommand Instance { get; } = new();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
