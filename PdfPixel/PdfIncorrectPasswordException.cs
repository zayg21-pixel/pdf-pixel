using System;

namespace PdfPixel;

public class PdfIncorrectPasswordException : Exception
{
    public PdfIncorrectPasswordException()
        : base("The provided password is incorrect.")
    {
    }

    public PdfIncorrectPasswordException(string message)
        : base(message)
    {
    }

    public PdfIncorrectPasswordException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
