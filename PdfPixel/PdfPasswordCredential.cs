namespace PdfPixel;

/// <summary>
/// User or owner password of a document encrypted with the Standard security handler.
/// </summary>
public sealed class PdfPasswordCredential : PdfCredential
{
    /// <summary>
    /// Initializes a credential with the given password.
    /// </summary>
    public PdfPasswordCredential(string password) => Password = password;

    /// <summary>
    /// The password.
    /// </summary>
    public string Password { get; }
}
