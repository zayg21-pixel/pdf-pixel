namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Permissions of a document, <c>{ print, modify, copy, annotate, fillForms, assemble, highQualityPrint }</c>.
/// </summary>
internal sealed class PermissionsDto
{
    public bool Print { get; set; }

    public bool Modify { get; set; }

    public bool Copy { get; set; }

    public bool Annotate { get; set; }

    public bool FillForms { get; set; }

    public bool Assemble { get; set; }

    public bool HighQualityPrint { get; set; }
}
