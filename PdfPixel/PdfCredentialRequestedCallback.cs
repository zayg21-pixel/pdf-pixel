namespace PdfPixel;

/// <summary>
/// Returns the credential for an encrypted document, or null to decline.
/// </summary>
/// <param name="request">Why and for which access the credential is requested.</param>
public delegate PdfCredential? PdfCredentialRequestedCallback(PdfCredentialRequest request);
