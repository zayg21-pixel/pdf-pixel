using System;

namespace PdfPixel.Encryption.Model;

/// <summary>
/// Thrown when a PDF document is encrypted and none of the supplied credentials is accepted.
/// </summary>
public class PdfAuthenticationException : Exception
{
    /// <summary>
    /// Initializes a new instance with the default message.
    /// </summary>
    public PdfAuthenticationException()
        : base("The provided credentials are not accepted.")
    {
    }

    /// <inheritdoc/>
    public PdfAuthenticationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public PdfAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
