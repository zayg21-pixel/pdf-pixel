namespace PdfReader.Models;

/// <summary>
/// Represents a reference to a PDF object stream, including offset, length, and encryption status.
/// </summary>
public struct PdfObjectStreamReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObjectStreamReference"/> struct.
    /// </summary>
    /// <param name="offset">The byte offset of the stream within the PDF file.</param>
    /// <param name="length">The length of the stream in bytes.</param>
    /// <param name="isEncrypted">Indicates whether the stream is encrypted.</param>
    public PdfObjectStreamReference(int offset, int length, bool isEncrypted)
    {
        Offset = offset;
        Length = length;
        IsEncrypted = isEncrypted;
    }

    /// <summary>
    /// Gets the byte offset of the stream within the PDF file.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Gets the length of the stream in bytes.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets a value indicating whether the stream is encrypted.
    /// </summary>
    public bool IsEncrypted { get; }
}
