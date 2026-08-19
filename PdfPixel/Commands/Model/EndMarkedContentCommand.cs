namespace PdfPixel.Commands.Model;

/// <summary>
/// Pops the most recent marked content scope from the execution context stack.
/// </summary>
public sealed class EndMarkedContentCommand : PdfCommand
{
    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.EndMarkedContent;
}
