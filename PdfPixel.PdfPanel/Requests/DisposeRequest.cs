namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to dispose resources and stop the rendering loop.
/// Processed as the final request before shutdown.
/// </summary>
public sealed class DisposeRequest : DrawingRequest
{
    public override bool IsSkippable => false;
}
