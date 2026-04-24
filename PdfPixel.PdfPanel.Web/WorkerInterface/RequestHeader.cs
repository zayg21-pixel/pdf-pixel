namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// Base class for container-related web requests.
/// </summary>
public class RequestHeader
{
    /// <summary>
    /// ID of container of web canvas.
    /// </summary>
    public string ContainerId { get; set; }
}
