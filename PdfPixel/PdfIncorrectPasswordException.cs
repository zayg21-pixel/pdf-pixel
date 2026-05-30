using System;

namespace PdfPixel;

/// <summary>
/// Thrown when a PDF document is encrypted and the supplied password is incorrect.
/// </summary>
public class PdfIncorrectPasswordException : Exception
{
    /// <summary>
    /// Initializes a new instance with the default message.
    /// </summary>
    public PdfIncorrectPasswordException()
        : base("The provided password is incorrect.")
    {
    }

    /// <inheritdoc/>
    public PdfIncorrectPasswordException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public PdfIncorrectPasswordException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
