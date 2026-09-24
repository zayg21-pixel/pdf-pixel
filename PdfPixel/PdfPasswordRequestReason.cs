namespace PdfPixel;

/// <summary>
/// Reasons an encrypted document requests a password.
/// </summary>
public enum PdfPasswordRequestReason
{
    /// <summary>
    /// The empty user password does not open the document.
    /// </summary>
    PasswordRequired,

    /// <summary>
    /// The previously supplied password is incorrect.
    /// </summary>
    IncorrectPassword
}
