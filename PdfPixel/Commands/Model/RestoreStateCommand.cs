namespace PdfPixel.Commands.Model;

/// <summary>
/// Restores the most recently saved state from the state stack.
/// </summary>
public sealed class RestoreStateCommand : PdfCommand
{
    private RestoreStateCommand()
    {
    }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.RestoreState;

    /// <summary>
    /// Default instance of <see cref="RestoreStateCommand"/>.
    /// </summary>
    public static RestoreStateCommand Instance { get; } = new();
}
