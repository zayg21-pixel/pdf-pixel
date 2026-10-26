namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Credential request of a document, <c>{ reason, authEvent }</c>.
/// </summary>
internal sealed class CredentialRequestDto
{
    public required string Reason { get; set; }

    public required string AuthEvent { get; set; }
}
