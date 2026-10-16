using System.Collections.Generic;

namespace PdfPixel.Files;

/// <summary>
/// An object that files can be associated with (PDF 2.0).
/// </summary>
public interface IPdfAssociatedFilesHost
{
    /// <summary>
    /// Files associated with the object (/AF), or <see langword="null"/> when absent.
    /// </summary>
    IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }
}
