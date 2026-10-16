using PdfPixel.Files;
using PdfPixel.Text;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable file attached to the document or to one of its pages.
/// </summary>
public sealed class PdfAttachmentViewModel
{
    internal PdfAttachmentViewModel(PdfFileSpecification fileSpecification, int? pageNumber)
    {
        FileSpecification = fileSpecification;
        PageNumber = pageNumber;
        Name = GetName(fileSpecification);
        Description = fileSpecification.Description?.DecodePdfString();
    }

    /// <summary>
    /// File specification of the attached file.
    /// </summary>
    public PdfFileSpecification FileSpecification { get; }

    /// <summary>
    /// Number of the page the file is attached to, or <see langword="null"/> when it is attached to the document.
    /// </summary>
    public int? PageNumber { get; }

    /// <summary>
    /// File name, or <see langword="null"/> when the document defines none.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Description of the file, or <see langword="null"/> when the document defines none.
    /// </summary>
    public string? Description { get; }

    private static string? GetName(PdfFileSpecification fileSpecification)
    {
        if (fileSpecification.UnicodeFile != null)
        {
            return fileSpecification.UnicodeFile.Value.DecodePdfString();
        }

        return fileSpecification.File?.DecodePdfString();
    }
}
