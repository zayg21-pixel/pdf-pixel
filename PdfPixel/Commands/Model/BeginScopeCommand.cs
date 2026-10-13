using PdfPixel.Tagging.Model;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Pushes a scope onto the execution context stack.
/// </summary>
public sealed class BeginScopeCommand : PdfCommand
{
    /// <summary>
    /// Initializes a new <see cref="BeginScopeCommand"/> with the specified scope.
    /// </summary>
    public BeginScopeCommand(PdfScope scope) => Scope = scope;

    /// <summary>
    /// The scope being entered.
    /// </summary>
    public PdfScope Scope { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.BeginScope;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(BeginScopeCommand)} {Scope}";
}
