namespace PdfPixel.Commands.Model;

/// <summary>
/// Saves the current state onto the state stack.
/// </summary>
public sealed class SaveStateCommand : PdfCommand
{
    private SaveStateCommand()
    {
    }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.SaveState;

    /// <summary>
    /// Default instance of <see cref="SaveStateCommand"/>.
    /// </summary>
    public static SaveStateCommand Instance { get; } = new();
}
