using PdfPixel.Models;

namespace PdfPixel.Files;

/// <summary>
/// A file related to an embedded file, from a related files array (RF, PDF 1.3).
/// </summary>
public sealed class PdfRelatedFile
{
    internal PdfRelatedFile(in PdfString name, PdfEmbeddedFile file)
    {
        Name = name;
        File = file;
    }

    /// <summary>
    /// Name of the related file.
    /// </summary>
    public PdfString Name { get; }

    /// <summary>
    /// Embedded file holding the related file's content.
    /// </summary>
    public PdfEmbeddedFile File { get; }
}
