namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// URI action, <c>{ type: 'uri', uri }</c>.
/// </summary>
internal sealed class UriActionDto : ActionDto
{
    public string? Uri { get; set; }
}
