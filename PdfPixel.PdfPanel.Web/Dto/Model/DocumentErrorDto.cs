namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Result of a document that failed to load, <c>{ ok: false, reason, message }</c>.
/// </summary>
internal sealed class DocumentErrorDto
{
    public bool Ok => false;

    public required string Reason { get; set; }

    public required string Message { get; set; }
}
