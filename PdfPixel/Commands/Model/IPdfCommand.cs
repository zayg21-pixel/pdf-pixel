namespace PdfPixel.Commands.Model;

/// <summary>
/// Represents a single drawing or state-management operation.
/// </summary>
public interface IPdfCommand
{
    /// <summary>
    /// Decoding capabilities of this command that determine when a cached picture must be regenerated.
    /// </summary>
    PdfCommandFeatures Features { get; }

    /// <summary>
    /// Identifies the concrete type of this command.
    /// </summary>
    PdfCommandKind Kind { get; }
}
