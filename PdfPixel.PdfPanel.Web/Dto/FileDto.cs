namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// File, <c>{ handle, name, description }</c>; the handle is null for a file that is not embedded.
/// </summary>
internal sealed class FileDto
{
    public int? Handle { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }
}
