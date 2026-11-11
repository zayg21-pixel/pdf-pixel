namespace PdfPixel.Commands;

/// <summary>
/// Represents a single drawing or state-management operation.
/// </summary>
public interface IPdfCommand
{
    /// <summary>
    /// Identifies the concrete type of this command.
    /// </summary>
    PdfCommandKind Kind { get; }
}
