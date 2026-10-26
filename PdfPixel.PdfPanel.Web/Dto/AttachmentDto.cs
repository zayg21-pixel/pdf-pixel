namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Attached file, <c>{ file, pageNumber }</c>.
/// </summary>
internal sealed class AttachmentDto
{
    public required FileDto File { get; set; }

    public int? PageNumber { get; set; }
}
