using PdfPixel.Navigation;
using System;
using System.IO;

namespace PdfPixel.Demo.Wpf.ViewModels;

/// <summary>
/// Document to open in a tab: its file name, its bytes, where it comes from and where to scroll to.
/// </summary>
public sealed class DocumentTabRequest
{
    public DocumentTabRequest(string fileName, byte[] data, string filePath, PdfDestination destination, DocumentTabRequest parent)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        FilePath = filePath;
        Destination = destination;
        Parent = parent;
    }

    /// <summary>
    /// File name without extension, shown when the document has no title.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Bytes of the PDF file.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Path of the file on disk, or null for a file embedded in another document.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Destination to scroll to once the document is open, or null.
    /// </summary>
    public PdfDestination Destination { get; }

    /// <summary>
    /// Request of the document this one is embedded in, or null when it is not embedded.
    /// </summary>
    public DocumentTabRequest Parent { get; }

    /// <summary>
    /// Reads the file at <paramref name="filePath"/> into a request.
    /// </summary>
    public static DocumentTabRequest FromFile(string filePath) => FromFile(filePath, null);

    /// <summary>
    /// Reads the file at <paramref name="filePath"/> into a request that scrolls to <paramref name="destination"/>.
    /// </summary>
    public static DocumentTabRequest FromFile(string filePath, PdfDestination destination)
        => new(Path.GetFileNameWithoutExtension(filePath), File.ReadAllBytes(filePath), filePath, destination, null);

    /// <summary>
    /// Copy of this request that scrolls to <paramref name="destination"/>.
    /// </summary>
    public DocumentTabRequest WithDestination(PdfDestination destination) => new(FileName, Data, FilePath, destination, Parent);
}
