namespace PdfPixel.Encryption.Model;

/// <summary>
/// Reasons an encrypted document requests a credential.
/// </summary>
public enum PdfCredentialRequestReason
{
    /// <summary>
    /// The document does not open without a credential.
    /// </summary>
    CredentialRequired,

    /// <summary>
    /// The previously supplied credential is not accepted.
    /// </summary>
    CredentialRejected
}
