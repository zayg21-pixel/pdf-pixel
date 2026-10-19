namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Result of a document that is not opened without a credential, <c>{ ok: false, reason, message, credentialRequest }</c>.
/// </summary>
internal sealed class CredentialRequestedResultDto
{
    public bool Ok => false;

    public required string Reason { get; set; }

    public required string Message { get; set; }

    public CredentialRequestDto? CredentialRequest { get; set; }
}
