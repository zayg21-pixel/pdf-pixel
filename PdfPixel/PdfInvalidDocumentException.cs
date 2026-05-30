using System;

namespace PdfPixel;

/// <summary>
/// Thrown when a PDF document cannot be parsed due to structural or format errors.
/// </summary>
public class PdfInvalidDocumentException : Exception
{
    /// <inheritdoc/>
    public PdfInvalidDocumentException()
    {
    }

    /// <inheritdoc/>
    public PdfInvalidDocumentException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public PdfInvalidDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
