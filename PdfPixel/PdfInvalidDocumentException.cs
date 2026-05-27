using System;

namespace PdfPixel;

public class PdfInvalidDocumentException : Exception
{
    public PdfInvalidDocumentException()
    {
    }

    public PdfInvalidDocumentException(string message)
        : base(message)
    {
    }

    public PdfInvalidDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
