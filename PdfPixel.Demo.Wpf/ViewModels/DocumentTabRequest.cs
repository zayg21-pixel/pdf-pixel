using System;
using System.IO;

namespace PdfPixel.Demo.Wpf.ViewModels;

/// <summary>
/// Document to open in a tab: its file name and its bytes.
/// </summary>
public sealed class DocumentTabRequest
{
    public DocumentTabRequest(string fileName, byte[] data)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        Data = data ?? throw new ArgumentNullException(nameof(data));
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
    /// Reads the file at <paramref name="filePath"/> into a request.
    /// </summary>
    public static DocumentTabRequest FromFile(string filePath)
        => new(Path.GetFileNameWithoutExtension(filePath), File.ReadAllBytes(filePath));
}
