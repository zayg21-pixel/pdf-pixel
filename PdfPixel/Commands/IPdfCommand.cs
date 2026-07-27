namespace PdfPixel.Commands;

/// <summary>
/// Represents a single drawing or state-management operation on a canvas.
/// </summary>
public interface IPdfCommand
{
    /// <summary>
    /// Executes this command. The canvas to draw on is obtained from <see cref="PdfCommandExecutionContext.Canvas"/>.
    /// </summary>
    /// <param name="executionContext">Execution-time context containing the canvas, rendering parameters, and cancellation.</param>
    void Execute(PdfCommandExecutionContext executionContext);

    /// <summary>
    /// Decoding capabilities of this command that determine when a cached picture must be regenerated.
    /// </summary>
    PdfCommandFeatures Features { get; }
}
