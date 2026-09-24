using PdfPixel.Encryption;

namespace PdfPixel;

/// <summary>
/// Returns the password for an encrypted document, or null to decline.
/// </summary>
/// <param name="reason">Why the password is requested.</param>
/// <param name="authEvent">Access that requires the password.</param>
public delegate string? PdfPasswordRequestedCallback(PdfPasswordRequestReason reason, PdfAuthEvent authEvent);
