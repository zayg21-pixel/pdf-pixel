namespace PdfPixel.Encryption.Model;

/// <summary>
/// Request for a credential that opens an encrypted document.
/// </summary>
public sealed class PdfCredentialRequest
{
    /// <summary>
    /// Initializes a request with its reason and the access that requires the credential.
    /// </summary>
    public PdfCredentialRequest(PdfCredentialRequestReason reason, PdfAuthEvent authEvent)
    {
        Reason = reason;
        AuthEvent = authEvent;
    }

    /// <summary>
    /// Why the credential is requested.
    /// </summary>
    public PdfCredentialRequestReason Reason { get; }

    /// <summary>
    /// Access that requires the credential.
    /// </summary>
    public PdfAuthEvent AuthEvent { get; }
}
