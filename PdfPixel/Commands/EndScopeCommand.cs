namespace PdfPixel.Commands;

/// <summary>
/// Pops the most recent scope from the execution context stack.
/// </summary>
public sealed class EndScopeCommand : PdfCommand
{
    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.EndScope;
}
